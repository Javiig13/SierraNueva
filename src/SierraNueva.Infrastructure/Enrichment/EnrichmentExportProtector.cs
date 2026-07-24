using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SierraNueva.Infrastructure.Persistence;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Enrichment;

public static class EnrichmentExportProtector
{
    private const string AdditionalAuthenticatedData =
        "SierraNueva/promotion-enrichment/v1";
    private const int MinimumRsaKeySize = 3072;
    private const int AesKeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    public static async Task GenerateKeyPairAsync(
        string privateKeyPath,
        string publicKeyPath,
        CancellationToken cancellationToken)
    {
        string fullPrivateKeyPath = Path.GetFullPath(privateKeyPath);
        if (File.Exists(fullPrivateKeyPath))
        {
            throw new InvalidOperationException(
                $"La clave privada ya existe: {fullPrivateKeyPath}");
        }

        using RSA rsa = RSA.Create(MinimumRsaKeySize);
        byte[] privateKey = rsa.ExportPkcs8PrivateKey();
        byte[] publicKey = rsa.ExportSubjectPublicKeyInfo();
        try
        {
            await WriteAtomicTextAsync(
                fullPrivateKeyPath,
                PemEncoding.WriteString("PRIVATE KEY", privateKey),
                cancellationToken);
            await WriteAtomicTextAsync(
                publicKeyPath,
                Convert.ToBase64String(publicKey),
                cancellationToken);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    fullPrivateKeyPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    public static async Task EncryptAsync(
        string inputPath,
        string outputPath,
        string publicKeyBase64,
        CancellationToken cancellationToken)
    {
        await EncryptWithAadAsync(
            inputPath,
            outputPath,
            publicKeyBase64,
            AdditionalAuthenticatedData,
            cancellationToken);
    }

    internal static async Task EncryptWithAadAsync(
        string inputPath,
        string outputPath,
        string publicKeyBase64,
        string additionalAuthenticatedData,
        CancellationToken cancellationToken)
    {
        string fullInputPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException(
                $"No existe el estado privado que cifrar: {fullInputPath}",
                fullInputPath);
        }

        byte[] publicKey = Convert.FromBase64String(publicKeyBase64.Trim());
        byte[] plainText = await File.ReadAllBytesAsync(fullInputPath, cancellationToken);
        byte[] aesKey = RandomNumberGenerator.GetBytes(AesKeySizeBytes);
        try
        {
            using RSA rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);
            if (bytesRead != publicKey.Length || rsa.KeySize < MinimumRsaKeySize)
            {
                throw new CryptographicException(
                    "La clave pública no es una clave RSA-3072 completa.");
            }

            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            byte[] tag = new byte[TagSizeBytes];
            byte[] cipherText = new byte[plainText.Length];
            byte[] aad = Encoding.UTF8.GetBytes(additionalAuthenticatedData);
            using (AesGcm aes = new(aesKey, TagSizeBytes))
            {
                aes.Encrypt(nonce, plainText, cipherText, tag, aad);
            }

            byte[] encryptedKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
            EncryptedEnrichmentEnvelope envelope = new()
            {
                EncryptedKey = Convert.ToBase64String(encryptedKey),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Aad = additionalAuthenticatedData,
                CipherText = Convert.ToBase64String(cipherText)
            };
            await WriteAtomicTextAsync(
                outputPath,
                JsonSerializer.Serialize(envelope, JsonDefaults.Compact) + "\n",
                cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey);
            CryptographicOperations.ZeroMemory(plainText);
        }
    }

    public static async Task DecryptAsync(
        string inputPath,
        string outputPath,
        string privateKeyPath,
        bool deletePrivateKey,
        CancellationToken cancellationToken)
    {
        await DecryptWithAadAsync(
            inputPath,
            outputPath,
            privateKeyPath,
            deletePrivateKey,
            AdditionalAuthenticatedData,
            cancellationToken);
    }

    internal static async Task DecryptWithAadAsync(
        string inputPath,
        string outputPath,
        string privateKeyPath,
        bool deletePrivateKey,
        string additionalAuthenticatedData,
        CancellationToken cancellationToken)
    {
        string fullInputPath = Path.GetFullPath(inputPath);
        string fullPrivateKeyPath = Path.GetFullPath(privateKeyPath);
        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException(
                $"No existe la exportación cifrada: {fullInputPath}",
                fullInputPath);
        }

        if (!File.Exists(fullPrivateKeyPath))
        {
            throw new FileNotFoundException(
                $"No existe la clave privada efímera: {fullPrivateKeyPath}",
                fullPrivateKeyPath);
        }

        EncryptedEnrichmentEnvelope envelope = JsonSerializer.Deserialize<
            EncryptedEnrichmentEnvelope>(
                await File.ReadAllTextAsync(fullInputPath, cancellationToken),
                JsonDefaults.Compact) ??
            throw new InvalidDataException("La exportación cifrada contiene null.");
        ValidateEnvelope(envelope, additionalAuthenticatedData);

        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(await File.ReadAllTextAsync(
            fullPrivateKeyPath,
            cancellationToken));
        if (rsa.KeySize < MinimumRsaKeySize)
        {
            throw new CryptographicException("La clave privada no alcanza RSA-3072.");
        }

        byte[] aesKey = rsa.Decrypt(
            Convert.FromBase64String(envelope.EncryptedKey),
            RSAEncryptionPadding.OaepSHA256);
        byte[] cipherText = Convert.FromBase64String(envelope.CipherText);
        byte[] plainText = new byte[cipherText.Length];
        try
        {
            byte[] aad = Encoding.UTF8.GetBytes(additionalAuthenticatedData);
            using (AesGcm aes = new(aesKey, TagSizeBytes))
            {
                aes.Decrypt(
                    Convert.FromBase64String(envelope.Nonce),
                    cipherText,
                    Convert.FromBase64String(envelope.Tag),
                    plainText,
                    aad);
            }

            using JsonDocument _ = JsonDocument.Parse(plainText);
            await WriteAtomicBytesAsync(outputPath, plainText, cancellationToken);
            if (deletePrivateKey)
            {
                File.Delete(fullPrivateKeyPath);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey);
            CryptographicOperations.ZeroMemory(plainText);
        }
    }

    private static void ValidateEnvelope(
        EncryptedEnrichmentEnvelope envelope,
        string additionalAuthenticatedData)
    {
        if (envelope.Version != 1 ||
            envelope.KeyAlgorithm != "RSA-OAEP-SHA256" ||
            envelope.ContentAlgorithm != "AES-256-GCM" ||
            envelope.Aad != additionalAuthenticatedData ||
            string.IsNullOrWhiteSpace(envelope.EncryptedKey) ||
            string.IsNullOrWhiteSpace(envelope.Nonce) ||
            string.IsNullOrWhiteSpace(envelope.Tag) ||
            string.IsNullOrWhiteSpace(envelope.CipherText))
        {
            throw new InvalidDataException(
                "La exportación no usa el sobre criptográfico esperado.");
        }
    }

    private static async Task WriteAtomicTextAsync(
        string path,
        string value,
        CancellationToken cancellationToken)
    {
        await WriteAtomicBytesAsync(
            path,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(value),
            cancellationToken);
    }

    private static async Task WriteAtomicBytesAsync(
        string path,
        byte[] value,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, value, cancellationToken);
            await AtomicFile.ReplaceAsync(temporary, fullPath, cancellationToken);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private sealed class EncryptedEnrichmentEnvelope
    {
        public int Version { get; init; } = 1;

        public string KeyAlgorithm { get; init; } = "RSA-OAEP-SHA256";

        public string ContentAlgorithm { get; init; } = "AES-256-GCM";

        public string Aad { get; init; } = AdditionalAuthenticatedData;

        public string EncryptedKey { get; init; } = string.Empty;

        public string Nonce { get; init; } = string.Empty;

        public string Tag { get; init; } = string.Empty;

        public string CipherText { get; init; } = string.Empty;
    }
}
