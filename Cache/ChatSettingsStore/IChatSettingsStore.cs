using MyUpdatedBot.Core.Models.Entities;

namespace MyUpdatedBot.Cache.ChatSettingsStore
{
    public interface IChatSettingsStore
    {
        bool TryGet(long chatId, out ChatSettingsEntity? settings);
        void Set(long chatId, ChatSettingsEntity settings);
    }
}
