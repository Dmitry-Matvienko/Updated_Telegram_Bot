using Microsoft.Extensions.Caching.Memory;
using MyUpdatedBot.Core.Models.Entities;

namespace MyUpdatedBot.Cache.ChatSettingsStore
{
    public class ChatSettingsStore : IChatSettingsStore
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan Sliding = TimeSpan.FromMinutes(15);

        public ChatSettingsStore(IMemoryCache cache) => _cache = cache;

        private static string Key(long chatId) => $"chatsettings:{chatId}";

        public bool TryGet(long chatId, out ChatSettingsEntity? settings)
        {
            return _cache.TryGetValue(Key(chatId), out settings);
        }

        public void Set(long chatId, ChatSettingsEntity settings)
        {
            var opts = new MemoryCacheEntryOptions { SlidingExpiration = Sliding };
            _cache.Set(Key(chatId), settings, opts);
        }
    }
}
