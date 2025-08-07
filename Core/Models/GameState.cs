using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Core.Models
{
    public class GameState
    {
        public long ChatId { get; }
        public long HostUserId { get; set; }
        public string CurrentWord { get; set; }

        // Callback that is called by timeout
        private CancellationTokenSource ?_timeoutCts;
        private readonly Func<long, Task> _onTimeoutAsync;

        public GameState(long chatId, long hostUserId, string word, Func<long, Task> onTimeout)
        {
            ChatId = chatId;
            HostUserId = hostUserId;
            CurrentWord = word;
            _onTimeoutAsync = onTimeout;
            ResetTimeout();
        }

        public void ResetTimeout()
        {
            // delete old
            _timeoutCts?.Cancel();
            _timeoutCts?.Dispose();

            // create new
            _timeoutCts = new CancellationTokenSource();

            _timeoutCts.Token.Register(() =>
            {
                _ = _onTimeoutAsync(ChatId);
            });

            _timeoutCts.CancelAfter(TimeSpan.FromMinutes(15));
        }
    }
}
