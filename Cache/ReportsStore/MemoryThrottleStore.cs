using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MyUpdatedBot.Cache.ReportsStore
{
    public class MemoryThrottleStore : IThrottleStore
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryThrottleStore> _logger;

        private readonly TimeSpan _throttleDelay;

        public MemoryThrottleStore(IMemoryCache cache, ILogger<MemoryThrottleStore> logger, TimeSpan? throttleDelay = null)
        {
            _cache = cache;
            _logger = logger;
            _throttleDelay = throttleDelay ?? TimeSpan.FromSeconds(180);
        }

        public bool TryCheckAndSet((long chat, long user) key, out int waitSeconds)
        {
            var cacheKey = GetKey(key);

            _logger.LogDebug("[MemoryThrottleStore]: TryCheckAndSet start for {CacheKey} ttl={Ttl}s", cacheKey, _throttleDelay.TotalSeconds);

            if (_cache.TryGetValue(cacheKey, out long expiryTicks))
            {
                var remaining = new DateTime(expiryTicks, DateTimeKind.Utc) - DateTime.UtcNow;
                waitSeconds = remaining.TotalSeconds > 0 ? (int) Math.Ceiling(remaining.TotalSeconds) : 0;

                _logger.LogInformation("[MemoryThrottleStore]: HIT {CacheKey} remaining={Seconds}s", cacheKey, waitSeconds);

                return false;
            }

            var expiry = DateTime.UtcNow.Add(_throttleDelay);
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _throttleDelay
            };

            _cache.Set(cacheKey, expiry.Ticks, options);
            waitSeconds = 0;
            return true;
        }

        private static string GetKey((long chat, long user) key) => $"throttle:{key.chat}:{key.user}";
    }
}
