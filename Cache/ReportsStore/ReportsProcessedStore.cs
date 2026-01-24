using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MyUpdatedBot.Cache.ReportsStore
{
    public class ReportsProcessedStore : IReportsProcessedStore, IDisposable
    {
        private readonly ConcurrentDictionary<(long, int, long), ProcessedInfo> _dict = new();
        private readonly IMemoryCache _cache;
        private readonly ILogger<ReportsProcessedStore> _logger;
        private volatile bool _disposed;
        private readonly TimeSpan _processedRetention;

        public ReportsProcessedStore(IMemoryCache cache, ILogger<ReportsProcessedStore> logger, TimeSpan? retention = null)
        {
            _cache = cache;
            _logger = logger;
            _processedRetention = retention ?? TimeSpan.FromDays(3);
        }

        public bool TryGet((long sourceChat, int sourceMessageId, long targetUser) key, out ProcessedInfo info)
        {
            ThrowIfDisposed();
            var tupleKey = (key.sourceChat, key.sourceMessageId, key.targetUser);

            var found = _dict.TryGetValue(tupleKey, out info);
            if (found)
            {
                _logger.LogDebug("[ReportsProcessedStore]: HIT for {Key}", GetKey(tupleKey));
            }
            else
            {
                _logger.LogDebug("[ReportsProcessedStore]: MISS for {Key}", GetKey(tupleKey));
            }

            return found;
        }

        public bool TryAdd((long sourceChat, int sourceMessageId, long targetUser) key, ProcessedInfo info)
        {
            ThrowIfDisposed();

            var tupleKey = (key.sourceChat, key.sourceMessageId, key.targetUser);

            _logger.LogDebug("[ReportsProcessedStore]: TryAdd start for {Key} retention={Retention}", GetKey(tupleKey), _processedRetention);

            if (!_dict.TryAdd(tupleKey, info))
            {
                _logger.LogDebug("[ReportsProcessedStore]: TryAdd failed – already present {Key}", GetKey(tupleKey));
                return false;
            }

            var cacheKey = GetKey(tupleKey);
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _processedRetention
            };

            options.RegisterPostEvictionCallback((k, v, reason, state) =>
            {
                try
                {
                    if (state is ValueTuple<long, int, long> stateKey)
                    {
                        _dict.TryRemove(stateKey, out _);
                        _logger.LogDebug("[ReportsProcessedStore]: Evicted {Key} reason={Reason}", k, reason);
                    }
                    else
                    {
                        _logger.LogWarning("[ReportsProcessedStore]: Evicted {Key} with unexpected state type", k);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ReportsProcessedStore]: Exception in eviction callback for {Key}", k);
                }
            }, tupleKey);

            try
            {
                _cache.Set(cacheKey, true, options);
                _logger.LogDebug("[ReportsProcessedStore]: Marked processed {Key} (retention {Retention})", cacheKey, _processedRetention);
                return true;
            }
            catch (Exception ex)
            {
                // rollback in case cache set failed
                _dict.TryRemove(tupleKey, out _);
                _logger.LogError(ex, "[ReportsProcessedStore]: Failed to set cache key {Key}. Rolled back in-memory mark", cacheKey);
                throw;
            }
        }

        public bool TryRemove((long sourceChat, int sourceMessageId, long targetUser) key, out ProcessedInfo info)
        {
            ThrowIfDisposed();

            var tupleKey = (key.sourceChat, key.sourceMessageId, key.targetUser);
            var removed = _dict.TryRemove(tupleKey, out info);
            if (removed)
            {
                try
                {
                    _cache.Remove(GetKey(tupleKey));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ReportsProcessedStore]: Failed to remove cache entry after TryRemove");
                }
            }
            return removed;
        }

        private static string GetKey((long, int, long) tupleKey) => $"processed:{tupleKey.Item1}:{tupleKey.Item2}:{tupleKey.Item3}";

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                try
                {
                    // snapshot keys to avoid concurrent modification issues
                    var keys = _dict.Keys.ToArray();
                    foreach (var k in keys)
                    {
                        _dict.TryRemove(k, out _);
                        // also remove corresponding cache entry 
                        try { _cache.Remove(GetKey(k)); } catch { }
                    }
                }
                catch
                {
                }
            }

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ReportsProcessedStore));
        }
    }
}
