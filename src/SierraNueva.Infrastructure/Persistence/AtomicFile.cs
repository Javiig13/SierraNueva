namespace SierraNueva.Infrastructure.Persistence;

internal static class AtomicFile
{
    private const int MaxAttempts = 5;

    public static async Task ReplaceAsync(
        string temporary,
        string destination,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(temporary, destination, overwrite: true);
                return;
            }
            catch (Exception exception) when (
                IsRetryable(exception) &&
                attempt < MaxAttempts)
            {
                int delayMilliseconds = 25 * (1 << (attempt - 1));
                await Task.Delay(delayMilliseconds, cancellationToken);
            }
        }
    }

    private static bool IsRetryable(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }
}
