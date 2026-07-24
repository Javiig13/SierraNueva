using SierraNueva.Core.Models;

namespace SierraNueva.Core.Discovery;

public static class OpportunityBackfillPlanner
{
    public const int MaximumBatchDays = 367;

    public static IReadOnlyList<OpportunityBackfillBatch> Plan(
        DateOnly from,
        DateOnly to,
        int batchDays = MaximumBatchDays)
    {
        if (from > to)
        {
            throw new InvalidDataException(
                "La fecha inicial del backfill no puede superar la final.");
        }

        if (batchDays is < 1 or > MaximumBatchDays)
        {
            throw new InvalidDataException(
                $"Cada lote debe abarcar entre 1 y {MaximumBatchDays} días inclusivos.");
        }

        List<OpportunityBackfillBatch> batches = [];
        int sequence = 1;
        int nextDay = from.DayNumber;
        while (nextDay <= to.DayNumber)
        {
            int lastDay = Math.Min(to.DayNumber, nextDay + batchDays - 1);
            batches.Add(new()
            {
                Sequence = sequence++,
                From = DateOnly.FromDayNumber(nextDay),
                To = DateOnly.FromDayNumber(lastDay)
            });
            nextDay = lastDay + 1;
        }

        return batches;
    }
}
