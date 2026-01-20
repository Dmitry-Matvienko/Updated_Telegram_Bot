using MyUpdatedBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MyUpdatedBot.Core.Models.Entities;

namespace MyUpdatedBot.Services.ChatSettings
{
    public class ChatSettingsService : IChatSettingsService
    {
        private readonly MyDbContext _db;

        public ChatSettingsService(MyDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        private static ChatSettingsEntity DefaultFor(long chatId) =>
            new ChatSettingsEntity { ChatId = chatId, SpamProtectionEnabled = true, LinksAllowed = false };

        public async Task<ChatSettingsEntity> GetOrCreateAsync(long chatId, CancellationToken ct)
        {
            var tracked = await _db.ChatSettings.FirstOrDefaultAsync(c => c.ChatId == chatId, ct);
            if (tracked != null) return tracked;

            // Create new
            var settings = DefaultFor(chatId);
            _db.ChatSettings.Add(settings);
            await _db.SaveChangesAsync(ct);
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
                }
                else
                {
                    settings.LinksAllowed = allowed;
                    _db.ChatSettings.Update(settings);
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return settings;
            }
            catch
            {
                try { await tx.RollbackAsync(ct); } catch { }
                throw;
            }
        }

        public async Task<ChatSettingsEntity> SetSpamProtectionAsync(long chatId, bool enabled, CancellationToken ct = default)
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
                }
                else
                {
                    settings.SpamProtectionEnabled = enabled;
                    _db.ChatSettings.Update(settings);
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return settings;
            }
            catch
            {
                try { await tx.RollbackAsync(ct); } catch { }
                throw;
            }
        }
    }
}
