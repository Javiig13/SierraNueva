using SierraNueva.Core.Abstractions;

namespace SierraNueva.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
