using MyUpdatedBot.Core.Models.Entities;

namespace MyUpdatedBot.Services.ChatSettings
{
    public interface IChatSettingsService
    {
        Task<ChatSettingsEntity> GetOrCreateAsync(long chatId, CancellationToken ct);
        Task<ChatSettingsEntity> SetLinksAllowedAsync(long chatId, bool allowed, CancellationToken ct);
        Task<ChatSettingsEntity> SetSpamProtectionAsync(long chatId, bool enabled, CancellationToken ct);
    }
}
