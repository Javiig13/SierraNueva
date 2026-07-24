using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Persistence;

namespace SierraNueva.Infrastructure.Enrichment;

public static partial class PromotionEnrichmentReviewReport
{
    public const string FileName = "promotion-enrichment-review.html";

    private static readonly IReadOnlyDictionary<string, string> FieldLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["address"] = "Dirección",
            ["postalCode"] = "Código postal",
            ["developerName"] = "Promotora",
            ["marketerName"] = "Comercializadora",
            ["cooperativeName"] = "Cooperativa",
            ["totalUnits"] = "Viviendas totales",
            ["availableUnits"] = "Viviendas disponibles",
            ["priceFrom"] = "Precio desde",
            ["priceTo"] = "Precio hasta",
            ["bedroomsMin"] = "Dormitorios mínimos",
            ["bedroomsMax"] = "Dormitorios máximos",
            ["bathroomsMin"] = "Baños mínimos",
            ["bathroomsMax"] = "Baños máximos",
            ["usableAreaMinSqm"] = "Superficie útil mínima",
            ["usableAreaMaxSqm"] = "Superficie útil máxima",
            ["builtAreaMinSqm"] = "Superficie construida mínima",
            ["builtAreaMaxSqm"] = "Superficie construida máxima",
            ["plotAreaMinSqm"] = "Parcela mínima",
            ["plotAreaMaxSqm"] = "Parcela máxima",
            ["garageSpacesMin"] = "Garajes mínimos",
            ["garageSpacesMax"] = "Garajes máximos",
            ["deliveryDateText"] = "Entrega",
            ["buildingLicenceStatus"] = "Licencia de obra"
        };

    public static string RenderText(EnrichmentState state, string stateDirectory)
    {
        IReadOnlyList<PromotionEnrichment> items = OrderedItems(state);
        StringBuilder output = new();
        AppendSummary(output, state);
        PromotionEnrichment[] pending = items
            .Where(item => item.Status == EnrichmentReviewStatus.Pending)
            .ToArray();
        if (pending.Length == 0)
        {
            output.AppendLine("No hay propuestas pendientes.");
            return output.ToString();
        }

        foreach (PromotionEnrichment item in pending)
        {
            output.AppendLine();
            output.AppendLine(
                CultureInfo.InvariantCulture,
                $"[{item.Id}] {Collapse(item.PromotionName)}");
            output.AppendLine(CultureInfo.InvariantCulture, $"  Ficha: {item.CanonicalUrl}");
            foreach (EnrichmentFieldProposal field in item.Fields)
            {
                output.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"  - {Label(field.Field)}: {Collapse(field.ValueText)} " +
                    $"({field.Confidence:P0}, {StatusLabel(field.Status)})");
                output.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    Cita: “{Collapse(field.EvidenceText)}”");
                output.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    Fuente: {field.SourceUrl}");
                if (field.Status == EnrichmentReviewStatus.Pending)
                {
                    output.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"    Aceptar: {ReviewCommand(stateDirectory, item.Id, field.Field, "accepted")}");
                    output.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"    Rechazar: {ReviewCommand(stateDirectory, item.Id, field.Field, "rejected")}");
                }
            }
        }

        return output.ToString();
    }

    public static async Task<string> WriteAsync(
        string stateDirectory,
        EnrichmentState state,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stateDirectory);
        string destination = Path.Combine(stateDirectory, FileName);
        string temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporary,
                RenderHtml(state, stateDirectory),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            await AtomicFile.ReplaceAsync(temporary, destination, cancellationToken);
            return destination;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public static string RenderHtml(EnrichmentState state, string stateDirectory)
    {
        IReadOnlyList<PromotionEnrichment> items = OrderedItems(state);
        PromotionEnrichment[] pending = items
            .Where(item => item.Status == EnrichmentReviewStatus.Pending)
            .ToArray();
        PromotionEnrichment[] reviewed = items
            .Where(item => item.Status != EnrichmentReviewStatus.Pending)
            .ToArray();
        StringBuilder html = new();
        html.AppendLine(
            """
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="referrer" content="no-referrer">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'">
              <title>Revisión privada · SierraNueva</title>
              <style>
                :root{color-scheme:light;--ink:#10231d;--muted:#64736d;--line:#dce5e1;--paper:#f4f7f5;--card:#fff;--brand:#07543e;--mint:#dff5ea;--warn:#fff0d8;--bad:#fee6e3}
                *{box-sizing:border-box}body{margin:0;background:var(--paper);color:var(--ink);font:15px/1.5 Inter,ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}
                main{width:min(1180px,calc(100% - 32px));margin:32px auto 64px}.top{display:grid;grid-template-columns:1fr auto;gap:24px;align-items:end;padding:28px;border-radius:24px;background:linear-gradient(135deg,#073f31,#0a6248);color:#fff;box-shadow:0 22px 60px #123c2b24}
                h1{margin:4px 0;font-size:clamp(28px,5vw,48px);letter-spacing:-.045em;line-height:1}.eyebrow{margin:0;color:#9fe6ca;font-size:12px;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.intro{max-width:720px;margin:12px 0 0;color:#d9eee6}.metrics{display:flex;gap:10px;flex-wrap:wrap}.metric{min-width:100px;padding:12px 14px;border:1px solid #ffffff24;border-radius:16px;background:#ffffff12}.metric strong{display:block;font-size:24px}.metric span{font-size:12px;color:#cce5dc}
                .notice{margin:18px 0;padding:14px 16px;border:1px solid #b9d9cd;border-radius:14px;background:var(--mint)}h2{margin:30px 0 14px;font-size:21px;letter-spacing:-.02em}.stack{display:grid;gap:16px}.proposal{overflow:hidden;border:1px solid var(--line);border-radius:20px;background:var(--card);box-shadow:0 12px 34px #173d2d0d}.proposal-head{display:flex;justify-content:space-between;gap:20px;padding:20px 22px;border-bottom:1px solid var(--line)}.proposal h3{margin:3px 0 4px;font-size:21px;letter-spacing:-.025em}.meta{margin:0;color:var(--muted);font-size:13px}.status{align-self:flex-start;padding:6px 10px;border-radius:999px;background:var(--warn);font-size:12px;font-weight:800}.fields{display:grid;grid-template-columns:repeat(auto-fit,minmax(310px,1fr));gap:1px;background:var(--line)}.field{padding:20px 22px;background:var(--card)}.field-top{display:flex;justify-content:space-between;gap:16px}.field h4{margin:0;color:var(--muted);font-size:12px;letter-spacing:.08em;text-transform:uppercase}.value{margin:6px 0 14px;font-size:24px;font-weight:760;letter-spacing:-.025em}.confidence{white-space:nowrap;color:var(--brand);font-weight:800}.current{margin:0 0 12px;color:var(--muted)}blockquote{margin:0 0 12px;padding:12px 14px;border-left:3px solid #52b993;border-radius:0 10px 10px 0;background:#f1f8f5}a{color:var(--brand);font-weight:700;word-break:break-all}.commands{display:grid;gap:8px;margin-top:16px}.command{padding:10px 12px;border-radius:10px;background:#11231d;color:#e8fff6;font:12px/1.45 ui-monospace,SFMono-Regular,Consolas,monospace;overflow:auto}.command b{color:#76e1b7}.warnings{margin:0;padding:14px 22px;background:var(--bad)}details{border:1px solid var(--line);border-radius:16px;background:#fff}summary{cursor:pointer;padding:16px 18px;font-weight:800}.reviewed-list{padding:0 18px 18px}.reviewed-item{display:flex;justify-content:space-between;gap:18px;padding:10px 0;border-top:1px solid var(--line)}.empty{padding:30px;border:1px dashed #a9bbb4;border-radius:18px;text-align:center;color:var(--muted)}
                @media(max-width:720px){main{width:min(100% - 20px,1180px);margin-top:10px}.top{grid-template-columns:1fr;padding:22px}.proposal-head{display:block}.status{display:inline-block;margin-top:10px}.fields{grid-template-columns:1fr}}
              </style>
            </head>
            <body>
            <main>
            """);
        html.AppendLine("<header class=\"top\"><div>");
        html.AppendLine("<p class=\"eyebrow\">SierraNueva · estado privado</p>");
        html.AppendLine("<h1>Revisión de propuestas</h1>");
        html.AppendLine(
            "<p class=\"intro\">Cada campo conserva su valor propuesto, la cita literal " +
            "y la fuente oficial. Revisa uno a uno; nada se publica desde este informe.</p>");
        html.AppendLine("</div><div class=\"metrics\">");
        AppendMetric(html, "Pendientes", PendingFieldCount(state).ToString(CultureInfo.InvariantCulture));
        AppendMetric(
            html,
            "Aceptadas",
            FieldCount(state, EnrichmentReviewStatus.Accepted).ToString(
                CultureInfo.InvariantCulture));
        AppendMetric(
            html,
            "Rechazadas",
            FieldCount(state, EnrichmentReviewStatus.Rejected).ToString(
                CultureInfo.InvariantCulture));
        html.AppendLine("</div></header>");
        html.AppendLine(
            "<p class=\"notice\"><strong>Seguro por diseño.</strong> “Actual” aparece " +
            "como sin dato porque el proveedor solo puede proponer campos vacíos. " +
            "La aceptación no sobrescribe extracción determinista y se aplica en el " +
            "siguiente crawl.</p>");
        html.AppendLine("<h2>Pendientes</h2><section class=\"stack\">");
        if (pending.Length == 0)
        {
            html.AppendLine("<div class=\"empty\">No hay propuestas pendientes.</div>");
        }
        else
        {
            foreach (PromotionEnrichment item in pending)
            {
                AppendProposal(html, item, stateDirectory);
            }
        }

        html.AppendLine("</section>");
        html.AppendLine("<h2>Historial de revisión</h2>");
        html.AppendLine("<details><summary>Ver propuestas resueltas u obsoletas</summary>");
        html.AppendLine("<div class=\"reviewed-list\">");
        if (reviewed.Length == 0)
        {
            html.AppendLine("<p class=\"meta\">Todavía no hay propuestas resueltas.</p>");
        }
        else
        {
            foreach (PromotionEnrichment item in reviewed)
            {
                html.Append("<div class=\"reviewed-item\"><span>");
                html.Append(Encode(item.PromotionName));
                html.Append("</span><strong>");
                html.Append(Encode(StatusLabel(item.Status)));
                html.AppendLine("</strong></div>");
            }
        }

        html.AppendLine("</div></details>");
        html.AppendLine("</main></body></html>");
        return html.ToString();
    }

    private static void AppendProposal(
        StringBuilder html,
        PromotionEnrichment item,
        string stateDirectory)
    {
        html.AppendLine("<article class=\"proposal\">");
        html.AppendLine("<header class=\"proposal-head\"><div>");
        html.Append("<p class=\"eyebrow\">");
        html.Append(Encode(item.Id));
        html.AppendLine("</p>");
        html.Append("<h3>");
        html.Append(Encode(item.PromotionName));
        html.AppendLine("</h3>");
        html.Append("<p class=\"meta\">Generada ");
        html.Append(Encode(item.GeneratedAtUtc.ToString("dd MMM yyyy · HH:mm 'UTC'", CultureInfo.GetCultureInfo("es-ES"))));
        html.Append(" · ");
        html.Append(Encode($"{item.Provider} / {item.Model}"));
        html.AppendLine("</p></div><span class=\"status\">Revisión pendiente</span></header>");
        html.AppendLine("<div class=\"fields\">");
        foreach (EnrichmentFieldProposal field in item.Fields)
        {
            AppendField(html, item, field, stateDirectory);
        }

        html.AppendLine("</div>");
        if (item.Warnings.Count > 0)
        {
            html.Append("<p class=\"warnings\"><strong>Avisos:</strong> ");
            html.Append(Encode(string.Join(" · ", item.Warnings)));
            html.AppendLine("</p>");
        }

        html.AppendLine("</article>");
    }

    private static void AppendField(
        StringBuilder html,
        PromotionEnrichment item,
        EnrichmentFieldProposal field,
        string stateDirectory)
    {
        html.AppendLine("<section class=\"field\">");
        html.Append("<div class=\"field-top\"><h4>");
        html.Append(Encode(Label(field.Field)));
        html.Append("</h4><span class=\"confidence\">");
        html.Append(Encode(field.Confidence.ToString("P0", CultureInfo.GetCultureInfo("es-ES"))));
        html.AppendLine("</span></div>");
        html.Append("<p class=\"value\">");
        html.Append(Encode(DisplayValue(field)));
        html.AppendLine("</p>");
        html.AppendLine("<p class=\"current\">Actual: <strong>sin dato</strong></p>");
        html.Append("<blockquote>“");
        html.Append(Encode(field.EvidenceText));
        html.AppendLine("”</blockquote>");
        if (Uri.TryCreate(field.SourceUrl, UriKind.Absolute, out Uri? source) &&
            source.Scheme is "http" or "https")
        {
            html.Append("<a href=\"");
            html.Append(Encode(source.AbsoluteUri));
            html.AppendLine("\" target=\"_blank\" rel=\"noopener noreferrer\">Abrir fuente oficial ↗</a>");
        }

        EnrichmentReviewStatus fieldStatus = EffectiveFieldStatus(item, field);
        if (fieldStatus == EnrichmentReviewStatus.Pending)
        {
            html.AppendLine("<div class=\"commands\">");
            AppendCommand(
                html,
                "Aceptar",
                ReviewCommand(stateDirectory, item.Id, field.Field, "accepted"));
            AppendCommand(
                html,
                "Rechazar",
                ReviewCommand(stateDirectory, item.Id, field.Field, "rejected"));
            html.AppendLine("</div>");
        }
        else
        {
            html.Append("<p class=\"status\">");
            html.Append(Encode(StatusLabel(fieldStatus)));
            html.AppendLine("</p>");
        }

        html.AppendLine("</section>");
    }

    private static void AppendCommand(StringBuilder html, string action, string command)
    {
        html.Append("<div class=\"command\"><b>");
        html.Append(Encode(action));
        html.Append(":</b> ");
        html.Append(Encode(command));
        html.AppendLine("</div>");
    }

    private static void AppendMetric(StringBuilder html, string label, string value)
    {
        html.Append("<div class=\"metric\"><strong>");
        html.Append(Encode(value));
        html.Append("</strong><span>");
        html.Append(Encode(label));
        html.AppendLine("</span></div>");
    }

    private static void AppendSummary(StringBuilder output, EnrichmentState state)
    {
        output.AppendLine(
            CultureInfo.InvariantCulture,
            $"Revisión IA privada: {PendingFieldCount(state)} campos pendientes, " +
            $"{FieldCount(state, EnrichmentReviewStatus.Accepted)} aceptados, " +
            $"{FieldCount(state, EnrichmentReviewStatus.Rejected)} rechazados.");
    }

    private static int PendingFieldCount(EnrichmentState state)
    {
        return FieldCount(state, EnrichmentReviewStatus.Pending);
    }

    private static int FieldCount(EnrichmentState state, EnrichmentReviewStatus status)
    {
        return state.Items.Sum(
            item => item.Fields.Count(field => EffectiveFieldStatus(item, field) == status));
    }

    private static EnrichmentReviewStatus EffectiveFieldStatus(
        PromotionEnrichment item,
        EnrichmentFieldProposal field)
    {
        bool legacyWholeProposal = item.ReviewedAtUtc is not null &&
                                   item.Fields.All(
                                       candidate =>
                                           candidate.Status == EnrichmentReviewStatus.Pending &&
                                           candidate.ReviewedAtUtc is null);
        return legacyWholeProposal ? item.Status : field.Status;
    }

    private static IReadOnlyList<PromotionEnrichment> OrderedItems(EnrichmentState state)
    {
        return state.Items
            .OrderBy(item => item.Status == EnrichmentReviewStatus.Pending ? 0 : 1)
            .ThenByDescending(item => item.GeneratedAtUtc)
            .ThenBy(item => item.PromotionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ReviewCommand(
        string stateDirectory,
        string proposalId,
        string field,
        string decision)
    {
        return "dotnet run --project src/SierraNueva.Crawler -c Release -- " +
               $"review-enrichment --state \"{stateDirectory}\" " +
               $"--proposal \"{proposalId}\" --field \"{field}\" --decision {decision}";
    }

    private static string DisplayValue(EnrichmentFieldProposal field)
    {
        if (field.Field is "priceFrom" or "priceTo" &&
            decimal.TryParse(
                field.ValueText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal price))
        {
            return $"{price.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))} €";
        }

        if (field.Field.Contains("Area", StringComparison.Ordinal) &&
            decimal.TryParse(
                field.ValueText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal area))
        {
            return $"{area.ToString("N0", CultureInfo.GetCultureInfo("es-ES"))} m²";
        }

        return field.ValueText;
    }

    private static string Label(string field)
    {
        return FieldLabels.TryGetValue(field, out string? label) ? label : field;
    }

    private static string StatusLabel(EnrichmentReviewStatus status)
    {
        return status switch
        {
            EnrichmentReviewStatus.Pending => "Pendiente",
            EnrichmentReviewStatus.Accepted => "Aceptada",
            EnrichmentReviewStatus.Rejected => "Rechazada",
            EnrichmentReviewStatus.Stale => "Obsoleta",
            _ => status.ToString()
        };
    }

    private static string Collapse(string value)
    {
        return Whitespace().Replace(Clean(value), " ").Trim();
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(Clean(value));
    }

    private static string Clean(string value)
    {
        return ControlCharacters().Replace(value, " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[\p{Cc}\p{Cf}]+")]
    private static partial Regex ControlCharacters();
}
