using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MyUpdatedBot.Cache.SpamStore
{
    public class FloodStore : IFloodStore
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<FloodStore> _logger;
        private readonly TimeSpan _window = TimeSpan.FromSeconds(4);
        private readonly int _limit = 3;
        private readonly TimeSpan _entryTtl;

        private static string BucketKey(long chatId, long userId) => $"tokenbucket:{chatId}:{userId}";
        private static string WarningCountKey(long chatId, long userId) => $"warncount:{chatId}:{userId}";

        public FloodStore(IMemoryCache cache, ILogger<FloodStore> logger, TimeSpan? entryTtl)
        {
            _cache = cache;
            _logger = logger;
            _entryTtl = entryTtl ?? TimeSpan.FromMicroseconds(15);
        }

        public Task<bool> AddAndCheckAsync(long chatId, long userId)
        {
            var key = BucketKey(chatId, userId);

            var bucket = _cache.GetOrCreate(key, entry =>
            {
                entry.SlidingExpiration = _entryTtl;
                _logger?.LogDebug("[FloodStore]: Creating TokenBucket for chat={Chat} user={User}", chatId, userId);
                return new TokenBucket(_limit, _window);
            });

            var allowed = bucket.TryConsume();
            return Task.FromResult(!allowed);
        }

        public void SetCachedWarningsCount(long chatId, long userId, int count)
        {
            var key = WarningCountKey(chatId, userId);
            _cache.Set(key, count, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(5) });
            _logger.LogDebug("[FloodStore]: Set cached warnings count for chat={Chat} user={User}: {Count}", chatId, userId, count);
        }

        private class TokenBucket
        {
            private readonly object _sync = new();
            private double _tokens;
            private long _lastRefillMs;
            private readonly int _capacity;
            private readonly double _tokensPerMs;

            public TokenBucket(int capacity, TimeSpan window)
            {
                _capacity = capacity;
                _tokens = capacity;
                _lastRefillMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _tokensPerMs = capacity / window.TotalMilliseconds; // double
            }

            //true - token allowed
            public bool TryConsume()
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                lock (_sync)
                {
                    var elapsed = now - _lastRefillMs;
                    if (elapsed > 0)
                    {
                        _tokens = Math.Min(_capacity, _tokens + elapsed * _tokensPerMs);
                        _lastRefillMs = now;
                    }

                    if (_tokens >= 1.0)
                    {
                        _tokens -= 1.0;
                        return true;
                    }

                    return false;
                }
            }
        }
    }
}
