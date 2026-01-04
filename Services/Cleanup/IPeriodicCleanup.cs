using System;

namespace MyUpdatedBot.Services.Cleanup
{
    public interface IPeriodicCleanup
    {
        string Name { get; }
        TimeSpan Interval { get; }
        Task CleanupAsync(CancellationToken cancellationToken);
    }
}
