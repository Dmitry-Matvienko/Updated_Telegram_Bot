using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Infrastructure.Data;

namespace MyUpdatedBot.Services.SpamProtection
{
    public class WarningService : IWarning
    {
        private readonly MyDbContext _db;
        private readonly ILogger<WarningService> _logger;
        private const int MaxRetries = 5;

        public WarningService(MyDbContext db, ILogger<WarningService> logger)
        {
            _db = db;
            _logger = logger;
        }
        public async Task<int> AddWarningAsync(long chatId, long telegramUserId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var attempt = 0;

            while (true)
            {
                attempt++;
                using var tx = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
                try
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == telegramUserId, ct).ConfigureAwait(false);
                    if (user is null)
                    {
                        _logger.LogInformation("[WarningService]: User {UserId} not found when adding warning for chat {ChatId}", telegramUserId, chatId);
                        return 0;
                    }

                    var record = await _db.WarningRecords
                        .FirstOrDefaultAsync(w => w.UserRefId == user.Id && w.ChatId == chatId, ct)
                        .ConfigureAwait(false);

                    if (record is null)
                    {
                        record = new WarningRecord
                        {
                            UserRefId = user.Id,
                            ChatId = chatId,
                            WarningsCount = 1,
                            CreatedAtUtc = now
                        };
                        _db.WarningRecords.Add(record);
                        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                        await tx.CommitAsync(ct).ConfigureAwait(false);
                        return record.WarningsCount;
                    }

                    record.WarningsCount += 1;
                    record.CreatedAtUtc = now;

                    _db.WarningRecords.Update(record);
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    await tx.CommitAsync(ct).ConfigureAwait(false);

                    return record.WarningsCount;
                }
                catch (DbUpdateConcurrencyException ex) when (attempt < MaxRetries)
                {
                    _logger.LogWarning(ex, "[WarningService]: Concurrency conflict in AddWarningAsync, attempt {Attempt}", attempt);
                    try { await tx.RollbackAsync(ct).ConfigureAwait(false); } catch { }
                    await Task.Delay(50 * attempt, ct).ConfigureAwait(false);
                    continue;
                }
                catch (Exception ex)
                {
                    try { await tx.RollbackAsync(ct).ConfigureAwait(false); } catch { }
                    _logger.LogError(ex, "[WarningService]: AddWarningAsync failed");
                    throw;
                }
            }
        }
    }
}
