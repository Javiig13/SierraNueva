using System.Net;
using System.Text;
using System.Text.Json;
using SierraNueva.Core.Models;
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
            "fixture-model");
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

        IReadOnlyList<EnrichmentFieldProposal> proposals = await provider.ProposeAsync(
            evidence,
            ["priceFrom"],
            CancellationToken.None);

        EnrichmentFieldProposal proposal = Assert.Single(proposals);
        Assert.Equal("priceFrom", proposal.Field);
        Assert.Equal("475000", proposal.ValueText);
        Assert.NotNull(handler.RequestJson);
        using JsonDocument request = JsonDocument.Parse(handler.RequestJson);
        JsonElement format = request.RootElement.GetProperty("text").GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.Equal(
            "Bearer fixture-key",
            handler.Authorization);
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
}
