using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Crawling;
using SierraNueva.Infrastructure.Discovery;
using SierraNueva.Infrastructure.Documents;
using SierraNueva.Infrastructure.Enrichment;
using SierraNueva.Infrastructure.Persistence;

namespace SierraNueva.Infrastructure.Tests;

public sealed class EnrichmentPipelineTests
{
    [Fact]
    public async Task OpenAiProvider_UsesStrictStructuredOutputAndParsesFixture()
    {
        string response = await File.ReadAllTextAsync(
            Path.Combine(
                AppContext.BaseDirectory,
                "test-data",
                "enrichment",
                "openai-response.json"));
        CapturingHandler handler = new(response);
        using HttpClient client = new(handler)
        {
            BaseAddress = new("https://api.openai.test/v1/")
        };
        OpenAiPromotionEnrichmentProvider provider = new(
            new SingleHttpClientFactory(client),
            "fixture-key",
            "gpt-5.6-luna");
        EnrichmentEvidenceDocument evidence = new()
        {
            PromotionId = "sn-cumbre",
            PromotionName = "Residencial Cumbre",
            Municipality = "Moralzarzal",
            CanonicalUrl = "https://fixtures.sierranueva.test/cumbre",
            Pages =
            [
                new()
                {
                    Url = "https://fixtures.sierranueva.test/cumbre",
                    Text = "Viviendas desde 475.000 euros"
                }
            ]
        };

        EnrichmentProviderResult result = await provider.ProposeAsync(
            evidence,
            ["priceFrom"],
            CancellationToken.None);

        EnrichmentFieldProposal proposal = Assert.Single(result.Fields);
        Assert.Equal("priceFrom", proposal.Field);
        Assert.Equal("475000", proposal.ValueText);
        Assert.Equal(1200, result.Usage.InputTokens);
        Assert.Equal(200, result.Usage.CachedInputTokens);
        Assert.Equal(80, result.Usage.OutputTokens);
        Assert.Equal(0.001_5m, result.Usage.EstimatedCostUsd);
        Assert.NotNull(handler.RequestJson);
        using JsonDocument request = JsonDocument.Parse(handler.RequestJson);
        JsonElement format = request.RootElement.GetProperty("text").GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.Equal(
            "low",
            request.RootElement.GetProperty("text").GetProperty("verbosity").GetString());
        Assert.False(request.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal(800, request.RootElement.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal(
            "none",
            request.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal(
            "Bearer fixture-key",
            handler.Authorization);
    }

    [Fact]
    public void OpenAiProvider_FailsClosedForModelWithoutAuditedPricing()
    {
        using HttpClient client = new()
        {
            BaseAddress = new("https://api.openai.test/v1/")
        };
        OpenAiPromotionEnrichmentProvider provider = new(
            new SingleHttpClientFactory(client),
            "fixture-key",
            "unpriced-model");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => provider.EstimateMaximumCost(
                new()
                {
                    PromotionName = "Fixture",
                    Municipality = "Moralzarzal"
                },
                ["priceFrom"]));

        Assert.Contains("tarifa auditada", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvidenceSource_BypassesConditionalCacheToRetainResponseBody()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"sierranueva-enrichment-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Uri url = new("https://fixtures.sierranueva.test/promotion");
            using FileHttpMetadataCache metadataCache = new(root);
            await metadataCache.SetAsync(
                url,
                "\"fixture-v1\"",
                new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero),
                CancellationToken.None);
            ConditionalHeaderHandler handler = new();
            using HttpClient client = new(handler);
            RespectfulPageSource pageSource = new(
                new SingleHttpClientFactory(client),
                [new ConfiguredUrlDiscoveryProvider()],
                new AllowAllUrlPolicy(),
                new InternalLinkDiscoveryProvider(),
                new NullDynamicPageRenderer(),
                new PdfPigTextExtractor(),
                metadataCache,
                NullLogger<RespectfulPageSource>.Instance);

            PageBatch batch = await pageSource.FetchAsync(
                new()
                {
                    Id = "enrichment-fixture",
                    Name = "Enrichment fixture",
                    BaseUrl = "https://fixtures.sierranueva.test/",
                    Enabled = true,
                    AllowedHosts = ["fixtures.sierranueva.test"],
                    StartUrls = [url.AbsoluteUri],
                    UseRobots = false,
                    UseSitemaps = false,
                    RequireResponseBody = true,
                    MaxPages = 1,
                    RequestDelayMilliseconds = 0
                },
                new()
                {
                    RequestDelayMilliseconds = 0,
                    MaxPagesPerSource = 1,
                    MaxRetries = 0
                },
                1,
                disablePlaywright: true,
                CancellationToken.None);

            Assert.Single(batch.Pages);
            Assert.False(handler.SentConditionalHeader);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Runner_SkipsCachedPromotionsBeforeApplyingCallLimit()
    {
        InMemoryEnrichmentRepository repository = new();
        RecordingProvider provider = new();
        PromotionEnrichmentRunner runner = new(
            new FixturePageSource(),
            repository,
            provider,
            new FixedClock());
        Promotion[] promotions =
        [
            CreatePromotion("sn-a", "A"),
            CreatePromotion("sn-b", "B")
        ];
        SourceDefinition[] sources = [CreateSource()];
        EnrichmentRunOptions options = new()
        {
            MaxPromotions = 1,
            MaxCostUsd = 0.05m,
            MaxEvidencePages = 1,
            MaxEvidenceCharacters = 2_000
        };

        EnrichmentRunResult first = await runner.RunAsync(
            [promotions[0]],
            sources,
            new(),
            "ignored",
            options,
            CancellationToken.None);
        EnrichmentRunResult second = await runner.RunAsync(
            promotions,
            sources,
            new(),
            "ignored",
            options,
            CancellationToken.None);

        Assert.Equal(1, first.ProcessedPromotions);
        Assert.Equal(1, second.CachedPromotions);
        Assert.Equal(1, second.ProcessedPromotions);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(2, repository.State.Runs.Count);
    }

    [Fact]
    public async Task Runner_DryRunIsFreeAndBudgetStopsBeforeNextCall()
    {
        InMemoryEnrichmentRepository repository = new();
        RecordingProvider provider = new()
        {
            MaximumCostPerCall = 0.04m,
            ActualCostPerCall = 0.02m
        };
        PromotionEnrichmentRunner runner = new(
            new FixturePageSource(),
            repository,
            provider,
            new FixedClock());
        Promotion[] promotions =
        [
            CreatePromotion("sn-a", "A"),
            CreatePromotion("sn-b", "B")
        ];
        EnrichmentRunOptions dryRun = new()
        {
            MaxPromotions = 2,
            MaxCostUsd = 0.05m,
            MaxEvidencePages = 1,
            MaxEvidenceCharacters = 2_000,
            DryRun = true
        };

        EnrichmentRunResult planned = await runner.RunAsync(
            promotions,
            [CreateSource()],
            new(),
            "ignored",
            dryRun,
            CancellationToken.None);

        Assert.Equal(0, provider.Calls);
        Assert.Equal(1, planned.PlannedPromotions);
        Assert.Equal(1, planned.BudgetSkippedPromotions);
        Assert.Equal(0, repository.Saves);

        EnrichmentRunResult executed = await runner.RunAsync(
            promotions,
            [CreateSource()],
            new(),
            "ignored",
            new()
            {
                MaxPromotions = 2,
                MaxCostUsd = 0.05m,
                MaxEvidencePages = 1,
                MaxEvidenceCharacters = 2_000
            },
            CancellationToken.None);

        Assert.Equal(1, provider.Calls);
        Assert.Equal(1, executed.ProcessedPromotions);
        Assert.Equal(1, executed.BudgetSkippedPromotions);
        Assert.Equal(0.02m, executed.Usage.EstimatedCostUsd);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task Runner_SendsOnlyBoundedRelevantEvidence()
    {
        RecordingProvider provider = new();
        string content =
            $"<html><body>{new string('x', 9_000)} " +
            "Entrega de llaves prevista para septiembre de 2027.</body></html>";
        PromotionEnrichmentRunner runner = new(
            new FixturePageSource(content),
            new InMemoryEnrichmentRepository(),
            provider,
            new FixedClock());

        EnrichmentRunResult result = await runner.RunAsync(
            [CreatePromotion("sn-a", "A")],
            [CreateSource()],
            new(),
            "ignored",
            new()
            {
                MaxPromotions = 1,
                MaxCostUsd = 0.05m,
                MaxEvidencePages = 1,
                MaxEvidenceCharacters = 1_000,
                DryRun = true
            },
            CancellationToken.None);

        EnrichmentEvidencePage page = Assert.Single(provider.LastEvidence!.Pages);
        Assert.True(page.Text.Length <= 1_000);
        Assert.Contains("Entrega de llaves prevista", page.Text, StringComparison.Ordinal);
        Assert.Equal(1, result.PlannedPromotions);
    }

    [Fact]
    public async Task PrivateRepository_WritesStableStateOutsidePublicData()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"sierranueva-enrichment-{Guid.NewGuid():N}");
        try
        {
            string stateDirectory = Path.Combine(root, "state");
            JsonEnrichmentStateRepository repository = new();
            await repository.SaveAsync(
                stateDirectory,
                new()
                {
                    GeneratedAtUtc = new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero),
                    Items =
                    [
                        new()
                        {
                            Id = "enr-b",
                            PromotionId = "sn-b",
                            PromotionName = "B"
                        },
                        new()
                        {
                            Id = "enr-a",
                            PromotionId = "sn-a",
                            PromotionName = "A"
                        }
                    ]
                },
                CancellationToken.None);

            string path = Path.Combine(
                stateDirectory,
                JsonEnrichmentStateRepository.FileName);
            Assert.True(File.Exists(path));
            Assert.False(Directory.Exists(Path.Combine(root, "public")));
            EnrichmentState loaded = await repository.LoadAsync(
                stateDirectory,
                CancellationToken.None);
            Assert.Equal(["sn-a", "sn-b"], loaded.Items.Select(item => item.PromotionId));
            Assert.Empty(Directory.GetFiles(stateDirectory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class CapturingHandler(string response) : HttpMessageHandler
    {
        public string? RequestJson { get; private set; }

        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestJson = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SingleHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class ConditionalHeaderHandler : HttpMessageHandler
    {
        public bool SentConditionalHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SentConditionalHeader =
                request.Headers.IfNoneMatch.Count > 0 ||
                request.Headers.IfModifiedSince is not null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<html><body>Viviendas desde 475.000 euros.</body></html>",
                    Encoding.UTF8,
                    "text/html")
            });
        }
    }

    private sealed class AllowAllUrlPolicy : IUrlPolicy
    {
        public bool IsAllowed(Uri url, SourceDefinition source, out SkipReason reason)
        {
            reason = SkipReason.None;
            return true;
        }
    }

    private sealed class NullDynamicPageRenderer : IDynamicPageRenderer
    {
        public Task<string?> RenderAsync(Uri url, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private static Promotion CreatePromotion(string id, string name)
    {
        return new()
        {
            Id = id,
            Name = name,
            Municipality = "Moralzarzal",
            CanonicalUrl = $"https://fixtures.sierranueva.test/{name.ToLowerInvariant()}",
            Active = true
        };
    }

    private static SourceDefinition CreateSource()
    {
        return new()
        {
            Id = "fixture",
            Name = "Fixture",
            BaseUrl = "https://fixtures.sierranueva.test/",
            Enabled = true,
            AllowedHosts = ["fixtures.sierranueva.test"],
            StartUrls = ["https://fixtures.sierranueva.test/"]
        };
    }

    private sealed class FixturePageSource(string? content = null) : IPageSource
    {
        public Task<PageBatch> FetchAsync(
            SourceDefinition source,
            CrawlerSettings settings,
            int? maxPagesOverride,
            bool disablePlaywright,
            CancellationToken cancellationToken)
        {
            string url = Assert.Single(source.StartUrls);
            return Task.FromResult(new PageBatch
            {
                Pages =
                [
                    new(
                        new(url),
                        content ??
                        $"<html><body>{source.Name}. Viviendas desde 475.000 euros.</body></html>",
                        "text/html",
                        new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero))
                ]
            });
        }
    }

    private sealed class InMemoryEnrichmentRepository : IEnrichmentStateRepository
    {
        public EnrichmentState State { get; private set; } = new();

        public int Saves { get; private set; }

        public Task<EnrichmentState> LoadAsync(
            string stateDirectory,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(State);
        }

        public Task SaveAsync(
            string stateDirectory,
            EnrichmentState queue,
            CancellationToken cancellationToken)
        {
            State = queue;
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProvider : IPromotionEnrichmentProvider
    {
        public int Calls { get; private set; }

        public decimal MaximumCostPerCall { get; init; } = 0.01m;

        public decimal ActualCostPerCall { get; init; } = 0.005m;

        public EnrichmentEvidenceDocument? LastEvidence { get; private set; }

        public string ProviderName => "fixture";

        public string Model => "fixture";

        public int MaxOutputTokens => 100;

        public EnrichmentCostEstimate EstimateMaximumCost(
            EnrichmentEvidenceDocument evidence,
            IReadOnlyList<string> missingFields)
        {
            LastEvidence = evidence;
            return new()
            {
                MaximumInputTokens = 1_000,
                MaximumOutputTokens = MaxOutputTokens,
                MaximumCostUsd = MaximumCostPerCall
            };
        }

        public Task<EnrichmentProviderResult> ProposeAsync(
            EnrichmentEvidenceDocument evidence,
            IReadOnlyList<string> missingFields,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new EnrichmentProviderResult
            {
                Fields =
                [
                    new()
                    {
                        Field = "priceFrom",
                        ValueText = "475000",
                        SourceUrl = evidence.CanonicalUrl,
                        EvidenceText = "Viviendas desde 475.000 euros",
                        Confidence = 0.96m
                    }
                ],
                Usage = new()
                {
                    InputTokens = 1_000,
                    OutputTokens = 100,
                    TotalTokens = 1_100,
                    EstimatedCostUsd = ActualCostPerCall
                }
            });
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow =>
            new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    }
}
