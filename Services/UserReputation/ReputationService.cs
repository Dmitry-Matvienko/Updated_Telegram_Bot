using Microsoft.EntityFrameworkCore;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Infrastructure.Data;

namespace MyUpdatedBot.Services.UserReputation
{
    public class ReputationService : IReputationService
    {
        private readonly MyDbContext _db;
        private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(3);

        public ReputationService(MyDbContext db) => _db = db;

        public async Task<bool> GiveReputationAsync(long fromUserId, long toUserId, long chatId, CancellationToken ct)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == toUserId, ct);
            if (user is null) return false;

            var now = DateTime.UtcNow;
            // Checking the cooldown for user 
            var given = await _db.ReputationGivens.SingleOrDefaultAsync(g => g.FromUserId == fromUserId && g.ToUserRefId == user.Id && g.ChatId == chatId, ct);
            if (given != null && (now - given.LastGiven) < Cooldown) return false;
            
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // atomically increment
                var rows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE RatingStats
                SET Rating = Rating + 1
                WHERE UserRefId = {user.Id} AND ChatId = {chatId};", ct);

                if (rows == 0)
                {
                    var newAgg = new ReputationEntity
                    {
                        UserRefId = user.Id,
                        ChatId = chatId,
                        Rating = 1
                    };
                    _db.RatingStats.Add(newAgg);
                }

                if (given == null)
                {
                    _db.ReputationGivens.Add(new ReputationGivenEntity
                    {
                        FromUserId = fromUserId,
                        ToUserRefId = user.Id,
                        ChatId = chatId,
                        LastGiven = now
                    });
                }
                else
                {
                    given.LastGiven = now;
                    _db.ReputationGivens.Update(given);
                }
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return true;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }

}
