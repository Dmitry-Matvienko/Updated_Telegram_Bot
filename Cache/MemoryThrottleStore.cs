using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MyUpdatedBot.Cache
{
    public class MemoryThrottleStore : IThrottleStore
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryThrottleStore> _logger;

        public MemoryThrottleStore(IMemoryCache cache, ILogger<MemoryThrottleStore> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public bool TryCheckAndSet((long chat, long user) key, TimeSpan throttleDelay, out int waitSeconds)
        {
            var cacheKey = GetKey(key);

            _logger.LogDebug("Throttle: TryCheckAndSet start for {CacheKey} ttl={Ttl}s", cacheKey, throttleDelay.TotalSeconds);

            if (_cache.TryGetValue(cacheKey, out long expiryTicks))
            {
                var remaining = new DateTime(expiryTicks, DateTimeKind.Utc) - DateTime.UtcNow;
                waitSeconds = remaining.TotalSeconds > 0 ? (int) Math.Ceiling(remaining.TotalSeconds) : 0;

                _logger.LogInformation("Throttle: HIT {CacheKey} remaining={Seconds}s", cacheKey, waitSeconds);

                return false;
            }

            var expiry = DateTime.UtcNow.Add(throttleDelay);
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = throttleDelay
            };

            _cache.Set(cacheKey, expiry.Ticks, options);
            waitSeconds = 0;
            return true;
        }

        private static string GetKey((long chat, long user) key) => $"throttle:{key.chat}:{key.user}";
    }
}
