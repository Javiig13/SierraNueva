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
    string model = "gpt-5.6-luna") : IPromotionEnrichmentProvider
{
    public string ProviderName => "openai-responses";

    public string Model { get; } = model;

    public async Task<IReadOnlyList<EnrichmentFieldProposal>> ProposeAsync(
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
        return parsed?.Fields ?? [];
    }

    internal object CreateRequest(
        EnrichmentEvidenceDocument evidence,
        IReadOnlyList<string> missingFields)
    {
        string evidenceJson = JsonSerializer.Serialize(evidence.Pages, JsonDefaults.Compact);
        return new
        {
            model = Model,
            instructions =
                """
                Extrae solo datos inmobiliarios que aparezcan explícitamente en la evidencia.
                No deduzcas, completes ni conviertas información ambigua. Devuelve únicamente
                campos solicitados. evidenceText debe ser una cita literal breve de la página y
                sourceUrl debe coincidir exactamente con la URL de esa página. Los números se
                devuelven sin unidades, separadores de miles ni símbolos y con punto decimal.
                Si no hay prueba suficiente, omite el campo.
                """,
            input =
                $"Promoción: {evidence.PromotionName}\n" +
                $"Municipio: {evidence.Municipality}\n" +
                $"Campos ausentes: {string.Join(", ", missingFields)}\n" +
                $"Evidencia JSON:\n{evidenceJson}",
            text = new
            {
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

    private sealed class EnrichmentResponse
    {
        public IReadOnlyList<EnrichmentFieldProposal> Fields { get; init; } = [];
    }
}
