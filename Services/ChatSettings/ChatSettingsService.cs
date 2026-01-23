using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Infrastructure.Data;

namespace MyUpdatedBot.Services.ChatSettings
{
    public class ChatSettingsService : IChatSettingsService
    {
        private readonly MyDbContext _db;
        private readonly ILogger<ChatSettingsService> _logger;

        public ChatSettingsService(MyDbContext db, ILogger<ChatSettingsService> logger)
        {
            _db = db;
            _logger = logger;
        }

        private static ChatSettingsEntity DefaultFor(long chatId) =>
            new ChatSettingsEntity { ChatId = chatId, SpamProtectionEnabled = false, LinksAllowed = true };

        public async Task<ChatSettingsEntity> GetOrCreateAsync(long chatId, CancellationToken ct)
        {
            var tracked = await _db.ChatSettings.FirstOrDefaultAsync(c => c.ChatId == chatId, ct);
            if (tracked != null)
            {
                _logger.LogDebug("[ChatSettingsService]: found existing ChatSettings for chat {ChatId}", chatId);
                return tracked;
            }

            // Create new
            var settings = DefaultFor(chatId);
            _db.ChatSettings.Add(settings);
            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("[ChatSettingsService]: created ChatSettings for chat {ChatId} (SpamProtection={Spam}, LinksAllowed={Links})",
                    chatId, settings.SpamProtectionEnabled, settings.LinksAllowed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChatSettingsService]: failed to create ChatSettings for chat {ChatId}", chatId);
                throw;
            }
            return settings;
        }

        public async Task<ChatSettingsEntity> SetLinksAllowedAsync(long chatId, bool allowed, CancellationToken ct)
        {
            using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var settings = await _db.ChatSettings.FirstOrDefaultAsync(c => c.ChatId == chatId, ct);
                if (settings == null)
                {
                    settings = DefaultFor(chatId);
                    settings.LinksAllowed = allowed;
                    _db.ChatSettings.Add(settings);

                    _logger.LogInformation("[ChatSettingsService]: SetLinksAllowedAsync for chat {ChatId} with LinksAllowed={Allowed}", chatId, allowed);
                }
                else
                {
                    settings.LinksAllowed = allowed;
                    _db.ChatSettings.Update(settings);

                    _logger.LogInformation("[ChatSettingsService]: updating ChatSettings for chat {ChatId}. LinksAllowed: {Allowed}", chatId, allowed);
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogDebug("[ChatSettingsService]: SetLinksAllowedAsync saved and committed ChatSettings for chat {ChatId}", chatId);
                return settings;
            }
            catch
            {
                try
                {
                    await tx.RollbackAsync(ct);
                    _logger.LogWarning("[ChatSettingsService]: transaction SetLinksAllowedAsync rolled back for chat {ChatId}", chatId);
                }
                catch { }
                throw;
            }
        }

        public async Task<ChatSettingsEntity> SetSpamProtectionAsync(long chatId, bool enabled, CancellationToken ct)
        {
            using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var settings = await _db.ChatSettings.FirstOrDefaultAsync(c => c.ChatId == chatId, ct);
                if (settings == null)
                {
                    settings = DefaultFor(chatId);
                    settings.SpamProtectionEnabled = enabled;
                    _db.ChatSettings.Add(settings);

                    _logger.LogInformation("[ChatSettingsService]: SetSpamProtectionAsync for chat {ChatId} with SpamProtectionEnabled={Enabled}", chatId, enabled);
                }
                else
                {
                    settings.SpamProtectionEnabled = enabled;
                    _db.ChatSettings.Update(settings);

                    _logger.LogInformation("[ChatSettingsService]: updating ChatSettings for chat {ChatId}. SpamProtectionEnabled: {Enabled}", chatId, enabled);
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogDebug("[ChatSettingsService]: SetSpamProtectionAsync saved and committed ChatSettings for chat {ChatId}", chatId);
                return settings;
            }
            catch
            {
                try
                {
                    await tx.RollbackAsync(ct);
                    _logger.LogWarning("[ChatSettingsService]: transaction SetSpamProtectionAsync rolled back for chat {ChatId}", chatId);
                }
                catch { }
                throw;
            }
        }
    }
}
