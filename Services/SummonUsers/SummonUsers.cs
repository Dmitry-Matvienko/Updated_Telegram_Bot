using MyUpdatedBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MyUpdatedBot.Services.SummonUsers
{
    public class SummonUsers : ISummonUsers
    {
        private readonly MyDbContext _db;

        public SummonUsers(MyDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<(long TelegramId, string DisplayName)>> GetUsersAsync(long chatId, int limit, CancellationToken ct = default)
        {
            var TakedUsers = _db.MessageStats
                .AsNoTracking()
                .Where(ms => ms.ChatId == chatId)
                .OrderByDescending(ms => ms.MessageCount)
                .Take(limit)
                .Select(ms => ms.User.UserId);

            var list = await TakedUsers.ToListAsync(ct);

            return list.Select(id => (TelegramId: id, DisplayName: string.Empty)).ToList();
        }
    }
}
