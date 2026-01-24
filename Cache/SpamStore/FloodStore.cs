using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MyUpdatedBot.Cache
{
    public class SpamStore : ISpamStore
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<SpamStore> _logger;
        private readonly TimeSpan _window = TimeSpan.FromSeconds(2);
        private readonly int _limit = 5;
        private static readonly TimeSpan EntryTtl = TimeSpan.FromSeconds(5);

        private static string SpamKey(long chatId, long userId) => $"spamwin:{chatId}:{userId}";
        private static string WarningCountKey(long chatId, long userId) => $"warncount:{chatId}:{userId}";

        public SpamStore(IMemoryCache cache, ILogger<SpamStore> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<bool> AddAndCheckAsync(long chatId, long userId, CancellationToken ct = default)
        {
            var key = SpamKey(chatId, userId);

            var window = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = EntryTtl;
                _logger.LogDebug("[SpamStore]: Creating SpamWindow for chat={Chat} user={User}", chatId, userId);
                return new SpamWindow(_limit, _window);
            });

            return await window.AddAndCheckAsync(ct).ConfigureAwait(false);
        }

        public void SetCachedWarningsCount(long chatId, long userId, int count)
        {
            var key = WarningCountKey(chatId, userId);
            _cache.Set(key, count, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5) });
            _logger.LogDebug("[SpamStore]: Set cached warnings count for chat={Chat} user={User}: {Count}", chatId, userId, count);
        }

        public int? TryGetCachedWarningsCount(long chatId, long userId)
        {
            var key = WarningCountKey(chatId, userId);
            if (_cache.TryGetValue<int>(key, out var v))
            {
                _logger.LogDebug("[SpamStore]: WarnCount cache hit for chat={Chat} user={User}: {Count}", chatId, userId, v);
                return v;
            }
            _logger.LogDebug("[SpamStore]: WarnCount cache miss for chat={Chat} user={User}", chatId, userId);
            return null;
        }

        // inner async-safe
        private class SpamWindow
        {
            private readonly SemaphoreSlim _sem = new(1, 1);
            private readonly Queue<long> _q = new();
            private readonly int _limit;
            private readonly long _windowTicks;

            public SpamWindow(int limit, TimeSpan window)
            {
                _limit = limit;
                _windowTicks = window.Ticks;
            }

            public async Task<bool> AddAndCheckAsync(CancellationToken ct = default)
            {
                await _sem.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var nowTicks = DateTime.UtcNow.Ticks;
                    var cutoff = nowTicks - _windowTicks;
                    while (_q.Count > 0 && _q.Peek() < cutoff) _q.Dequeue();
                    _q.Enqueue(nowTicks);
                    if (_q.Count >= _limit)
                    {
                        _q.Clear(); // prevent immediate retrigger
                        return true;
                    }
                    return false;
                }
                finally
                {
                    _sem.Release();
                }
            }
        }
    }
}
