using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace MyUpdatedBot.Cache
{
    public class ReportsProcessedStore : IProcessedStore, IDisposable
    {
        private readonly ConcurrentDictionary<(long, int, long), ProcessedInfo> _dict = new();
        private readonly IMemoryCache _cache;
        private volatile bool _disposed;

        public ReportsProcessedStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool TryGet((long sourceChat, int sourceMessageId, long targetUser) key, out ProcessedInfo info)
        {
            ThrowIfDisposed();
            return _dict.TryGetValue((key.sourceChat, key.sourceMessageId, key.targetUser), out info);
        }

        public bool TryAdd((long sourceChat, int sourceMessageId, long targetUser) key, ProcessedInfo info, TimeSpan retention)
        {
            ThrowIfDisposed();

            var tupleKey = (key.sourceChat, key.sourceMessageId, key.targetUser);
            if (!_dict.TryAdd(tupleKey, info))
                return false;

            var cacheKey = GetKey(tupleKey);
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = retention
            };

            options.RegisterPostEvictionCallback((k, v, reason, state) =>
            {
                if (state is ValueTuple<long, int, long> tk)
                    _dict.TryRemove(tk, out _);
            }, tupleKey);

            try
            {
                _cache.Set(cacheKey, true, options);
                return true;
            }
            catch
            {
                // rollback in case cache set failed
                _dict.TryRemove(tupleKey, out _);
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
                _cache.Remove(GetKey(tupleKey));
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
