using System.Collections.Concurrent;

namespace MyUpdatedBot.Core.Models
{
    public class RollResult
    {
        public long UserId { get; init; }
        public string? FirstName { get; init; }
        public int Value { get; init; }
        public DateTime At { get; init; } = DateTime.UtcNow;
    }
    public class RollGameState : IDisposable
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public long ChatId { get; }
        public long HostUserId { get; }
        public int MessageId { get; set; }
        public DateTime StartedAt { get; } = DateTime.UtcNow;
        public TimeSpan Duration { get; }
        public CancellationTokenSource? Cts { get; private set; }
        public ConcurrentDictionary<long, RollResult> Results { get; } = new();
        public SemaphoreSlim EditLock { get; } = new SemaphoreSlim(1, 1);

        private readonly Func<RollGameState, bool, Task> _onTimeoutAsync;

        public RollGameState(long chatId, long hostUserId, TimeSpan duration, Func<RollGameState, bool, Task> onTimeoutAsync)
        {
            ChatId = chatId;
            HostUserId = hostUserId;
            Duration = duration;
            _onTimeoutAsync = onTimeoutAsync;
            StartTimeout();
        }

        public DateTime EndsAt => StartedAt.Add(Duration);

        private void StartTimeout()
        {
            try
            {
                Cts?.Cancel();
                Cts?.Dispose();
            }
            catch { }

            Cts = new CancellationTokenSource();

            // Register is synchronously in ThreadPool, so launch an async handler.
            Cts.Token.Register(() =>
            {
                var task = _onTimeoutAsync(this, true);
                task.ContinueWith(t =>
                {
                }, TaskContinuationOptions.OnlyOnFaulted);
            });

            Cts.CancelAfter(Duration);
        }

        public void ResetTimeout() => StartTimeout();

        public void CancelTimeout()
        {
            try { Cts?.Cancel(); }
            catch { }
        }

        public void Dispose()
        {
            try { Cts?.Cancel(); } catch { }
            try { Cts?.Dispose(); } catch { }
            try { EditLock?.Dispose(); } catch { }
        }
    }
}
