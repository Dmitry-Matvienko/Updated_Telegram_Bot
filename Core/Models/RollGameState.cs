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

        private CancellationTokenRegistration _timeoutRegistration;
        private readonly Func<RollGameState, bool, Task> _onTimeoutAsync;

        public RollGameState(long chatId, long hostUserId, TimeSpan duration, Func<RollGameState, bool, Task> onTimeoutAsync)
        {
            ChatId = chatId;
            HostUserId = hostUserId;
            Duration = duration;
            _onTimeoutAsync = onTimeoutAsync ?? throw new ArgumentNullException(nameof(onTimeoutAsync));
            StartTimeout();
        }

        public DateTime EndsAt => StartedAt.Add(Duration);

        private void StartTimeout()
        {
            // unsubscribe the previous registration if there was one and cancel/dispose of the old CTS.
            try { _timeoutRegistration.Dispose(); } catch { }
            try { Cts?.Cancel(); } catch { }
            try { Cts?.Dispose(); } catch { }

            Cts = new CancellationTokenSource();

            // keep the registration so that can unsubscribe when the game end manually
            _timeoutRegistration = Cts.Token.Register(() =>
            {
                // fire and forget
                var t = _onTimeoutAsync(this, true);
                t.ContinueWith(ct =>
                {
                    var ignored = ct.Exception;
                }, TaskContinuationOptions.OnlyOnFaulted);
            });

            Cts.CancelAfter(Duration);
        }

        public void ResetTimeout() => StartTimeout();

        public void CancelTimeout()
        {
            try { _timeoutRegistration.Dispose(); } catch { }
            try { Cts?.Cancel(); } catch { }
            try { Cts?.Dispose(); } catch { }
        }

        public void Dispose()
        {
            try { _timeoutRegistration.Dispose(); } catch { }
            try { Cts?.Cancel(); } catch { }
            try { Cts?.Dispose(); } catch { }
            try { EditLock?.Dispose(); } catch { }
        }
    }
}