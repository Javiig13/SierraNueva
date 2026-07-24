using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Enrichment;

public sealed class OpenAiPromotionEnrichmentProvider(
    IHttpClientFactory httpClientFactory,
    string apiKey,
    string model = "gpt-5.6-luna",
    int maxOutputTokens = 800,
    string reasoningEffort = "none") : IPromotionEnrichmentProvider
{
    public string ProviderName => "openai-responses";

    public string Model { get; } = model;

    public int MaxOutputTokens { get; } = maxOutputTokens;

    public EnrichmentCostEstimate EstimateMaximumCost(
        EnrichmentEvidenceDocument evidence,
        IReadOnlyList<string> missingFields)
    {
        byte[] request = JsonSerializer.SerializeToUtf8Bytes(
            CreateRequest(evidence, missingFields),
            JsonDefaults.Compact);
        int maximumInputTokens = checked(request.Length + 256);
        ModelPricing pricing = PricingFor(Model);
        decimal maximumCost =
            maximumInputTokens * pricing.InputPerMillion / 1_000_000m +
            MaxOutputTokens * pricing.OutputPerMillion / 1_000_000m;
        return new()
        {
            MaximumInputTokens = maximumInputTokens,
            MaximumOutputTokens = MaxOutputTokens,
            MaximumCostUsd = decimal.Round(maximumCost, 6, MidpointRounding.AwayFromZero)
        };
    }

    public async Task<EnrichmentProviderResult> ProposeAsync(
        EnrichmentEvidenceDocument evidence,
        IReadOnlyList<string> missingFields,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Falta OPENAI_API_KEY. El enriquecimiento IA es opcional y no se ejecuta sin clave.");
        }

        using HttpRequestMessage request = new(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(CreateRequest(evidence, missingFields), options: JsonDefaults.Compact);
        HttpClient client = httpClientFactory.CreateClient("enrichment");
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI devolvió {(int)response.StatusCode}: {ReadError(json)}");
        }

        using JsonDocument document = JsonDocument.Parse(json);
        string output = ReadOutputText(document.RootElement);
        EnrichmentResponse? parsed = JsonSerializer.Deserialize<EnrichmentResponse>(
            output,
            JsonDefaults.Compact);
        return new()
        {
            Fields = parsed?.Fields ?? [],
            Usage = ReadUsage(document.RootElement)
        };
    }

    internal object CreateRequest(
        EnrichmentEvidenceDocument evidence,
        IReadOnlyList<string> missingFields)
    {
        string evidenceJson = JsonSerializer.Serialize(evidence.Pages, JsonDefaults.Compact);
        return new
        {
            model = Model,
            store = false,
            max_output_tokens = MaxOutputTokens,
            reasoning = new
            {
                effort = reasoningEffort
            },
            instructions =
                """
                Extrae solo datos inmobiliarios que aparezcan explícitamente en la evidencia.
                No deduzcas, completes ni conviertas información ambigua. Devuelve únicamente
                campos solicitados. evidenceText debe ser una cita literal breve de la página y
                sourceUrl debe coincidir exactamente con la URL de esa página. Los números se
                devuelven sin unidades, separadores de miles ni símbolos y con punto decimal.
                totalUnits es el total construido o anunciado; availableUnits solo es el número
                explícitamente disponible o restante, nunca el tamaño total de la promoción.
                priceFrom corresponde a "desde" o al extremo inferior; priceTo exige un máximo
                o extremo superior explícito y nunca puede obtenerse de un único precio "desde".
                cooperativeName exige un nombre propio o razón social; "cooperativa" o
                "régimen de cooperativa" por sí solos no son un nombre. Un valor exacto por
                vivienda puede completar los campos mínimo y máximo equivalentes.
                Si no hay prueba suficiente, omite el campo.
                """,
            input =
                $"Promoción: {evidence.PromotionName}\n" +
                $"Municipio: {evidence.Municipality}\n" +
                $"Campos ausentes: {string.Join(", ", missingFields)}\n" +
                $"Evidencia JSON:\n{evidenceJson}",
            text = new
            {
                verbosity = "low",
                format = new
                {
                    type = "json_schema",
                    name = "promotion_enrichment",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            fields = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        field = new
                                        {
                                            type = "string",
                                            @enum = missingFields
                                        },
                                        valueText = new { type = "string" },
                                        sourceUrl = new { type = "string" },
                                        evidenceText = new { type = "string" },
                                        confidence = new
                                        {
                                            type = "number",
                                            minimum = 0,
                                            maximum = 1
                                        }
                                    },
                                    required = new[]
                                    {
                                        "field",
                                        "valueText",
                                        "sourceUrl",
                                        "evidenceText",
                                        "confidence"
                                    },
                                    additionalProperties = false
                                }
                            }
                        },
                        required = new[] { "fields" },
                        additionalProperties = false
                    }
                }
            }
        };
    }

    private EnrichmentUsage ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out JsonElement usage))
        {
            return new();
        }

        int input = ReadInt(usage, "input_tokens");
        int output = ReadInt(usage, "output_tokens");
        int total = ReadInt(usage, "total_tokens");
        int cached = 0;
        int cacheWrite = 0;
        if (usage.TryGetProperty("input_tokens_details", out JsonElement inputDetails))
        {
            cached = ReadInt(inputDetails, "cached_tokens");
            cacheWrite = ReadInt(inputDetails, "cache_write_tokens");
        }

        int reasoning = 0;
        if (usage.TryGetProperty("output_tokens_details", out JsonElement outputDetails))
        {
            reasoning = ReadInt(outputDetails, "reasoning_tokens");
        }

        ModelPricing pricing = PricingFor(Model);
        int uncached = Math.Max(0, input - cached - cacheWrite);
        decimal cost =
            uncached * pricing.InputPerMillion / 1_000_000m +
            cached * pricing.CachedInputPerMillion / 1_000_000m +
            cacheWrite * pricing.InputPerMillion * 1.25m / 1_000_000m +
            output * pricing.OutputPerMillion / 1_000_000m;
        return new()
        {
            InputTokens = input,
            CachedInputTokens = cached,
            CacheWriteTokens = cacheWrite,
            OutputTokens = output,
            ReasoningTokens = reasoning,
            TotalTokens = total == 0 ? input + output : total,
            EstimatedCostUsd = decimal.Round(cost, 6, MidpointRounding.AwayFromZero)
        };
    }

    private static string ReadOutputText(JsonElement root)
    {
        foreach (JsonElement output in root.GetProperty("output").EnumerateArray())
        {
            if (!output.TryGetProperty("content", out JsonElement content))
            {
                continue;
            }

            foreach (JsonElement item in content.EnumerateArray())
            {
                if (item.TryGetProperty("text", out JsonElement text))
                {
                    return text.GetString() ?? throw new InvalidDataException(
                        "La respuesta estructurada de OpenAI está vacía.");
                }
            }
        }

        throw new InvalidDataException(
            "La respuesta de OpenAI no contiene texto estructurado.");
    }

    private static string ReadError(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement
                       .GetProperty("error")
                       .GetProperty("message")
                       .GetString() ??
                   "error sin detalle";
        }
        catch (JsonException)
        {
            return "respuesta de error no válida";
        }
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) &&
               value.TryGetInt32(out int parsed)
            ? parsed
            : 0;
    }

    private static ModelPricing PricingFor(string model)
    {
        return model switch
        {
            "gpt-5.6-luna" => new(1m, 0.1m, 6m),
            "gpt-5.6-terra" => new(2.5m, 0.25m, 15m),
            "gpt-5.6-sol" or "gpt-5.6" => new(5m, 0.5m, 30m),
            _ => throw new InvalidOperationException(
                $"No hay una tarifa auditada para el modelo '{model}'. " +
                "Actualiza el catálogo antes de usarlo para conservar el límite de coste.")
        };
    }

    private sealed class EnrichmentResponse
    {
        public IReadOnlyList<EnrichmentFieldProposal> Fields { get; init; } = [];
    }

    private sealed record ModelPricing(
        decimal InputPerMillion,
        decimal CachedInputPerMillion,
        decimal OutputPerMillion);
}
