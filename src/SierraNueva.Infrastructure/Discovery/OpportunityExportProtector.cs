using SierraNueva.Infrastructure.Enrichment;

namespace SierraNueva.Infrastructure.Discovery;

public static class OpportunityExportProtector
{
    private const string AdditionalAuthenticatedData =
        "SierraNueva/opportunity-triage/v1";

    public static Task GenerateKeyPairAsync(
        string privateKeyPath,
        string publicKeyPath,
        CancellationToken cancellationToken)
    {
        return EnrichmentExportProtector.GenerateKeyPairAsync(
            privateKeyPath,
            publicKeyPath,
            cancellationToken);
    }

    public static Task EncryptAsync(
        string inputPath,
        string outputPath,
        string publicKeyBase64,
        CancellationToken cancellationToken)
    {
        return EnrichmentExportProtector.EncryptWithAadAsync(
            inputPath,
            outputPath,
            publicKeyBase64,
            AdditionalAuthenticatedData,
            cancellationToken);
    }

    public static Task DecryptAsync(
        string inputPath,
        string outputPath,
        string privateKeyPath,
        bool deletePrivateKey,
        CancellationToken cancellationToken)
    {
        return EnrichmentExportProtector.DecryptWithAadAsync(
            inputPath,
            outputPath,
            privateKeyPath,
            deletePrivateKey,
            AdditionalAuthenticatedData,
            cancellationToken);
    }
}
