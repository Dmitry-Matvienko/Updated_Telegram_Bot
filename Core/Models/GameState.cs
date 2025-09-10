namespace MyUpdatedBot.Core.Models
{
    public class GameState : IDisposable
    {
        public long ChatId { get; }
        public long HostUserId { get; set; }
        public string CurrentWord { get; set; }

        private CancellationTokenSource? Cts;
        private CancellationTokenRegistration _timeoutRegistration;
        private readonly Func<long, Task> _onTimeoutAsync;
        private readonly TimeSpan _timeout;

        public GameState(long chatId, long hostUserId, string word, Func<long, Task> onTimeout, TimeSpan? timeout = null)
        {
            ChatId = chatId;
            HostUserId = hostUserId;
            CurrentWord = word;
            _onTimeoutAsync = onTimeout;
            _timeout = timeout ?? TimeSpan.FromMinutes(1);
            ResetTimeout();
        }

        public void ResetTimeout()
        {
            // unsubscribe the previous registration if there was one and cancel/dispose of the old CTS.
            try { _timeoutRegistration.Dispose(); } catch { }
            try { Cts?.Cancel(); } catch { }
            try { Cts?.Dispose(); } catch { }

            Cts = new CancellationTokenSource();

            // keep the registration so that can unsubscribe when the game end manually
            _timeoutRegistration = Cts.Token.Register(() =>
            {
                var task = _onTimeoutAsync(ChatId);
                task.ContinueWith(t =>
                {
                    var ignored = t.Exception;
                }, TaskContinuationOptions.OnlyOnFaulted);
            });

            Cts.CancelAfter(_timeout);
        }

        // called when manually closinh. unsubscribe before canceling
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
        }
    }
}