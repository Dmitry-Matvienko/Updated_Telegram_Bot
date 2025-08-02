using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Services.UserLeaderboard
{
    internal class UserLeaderboardService : IUserLeaderboardService
    {
        private readonly MyDbContext _db;
        private readonly ILogger<UserLeaderboardService> _logger;

        public UserLeaderboardService(MyDbContext db, ILogger<UserLeaderboardService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<string> TopTen(long? chatIdFilter, bool isRating, long myTelegramId, CancellationToken ct)
        {
            // Making both tables into a single anonymous type StatDto.
            IQueryable<StatDto> stats = isRating
                ? _db.RatingStats
                    .AsNoTracking()
                    .Select(r => new StatDto { UserRefId = r.UserRefId, ChatId = r.ChatId, Count = r.Rating })
                : _db.MessageStats
                    .AsNoTracking()
                    .Select(m => new StatDto { UserRefId = m.UserRefId, ChatId = m.ChatId, Count = m.MessageCount });

            // Chat filter
            if (chatIdFilter.HasValue)
                stats = stats.Where(s => s.ChatId == chatIdFilter.Value);

            // Top-10
            var topGroup = stats
                .GroupBy(s => s.UserRefId)
                .Select(g => new { UserRefId = g.Key, TotalCount = g.Sum(s => s.Count) })
                .OrderByDescending(x => x.TotalCount)
                .Take(10);

            // Join with users
            var top = await topGroup
                .Join(_db.Users,
                      stat => stat.UserRefId,
                      u => u.Id,
                      (stat, u) => new {
                          TelegramId = u.UserId,
                          Display = u.FirstName ?? u.Username ?? "–",
                          Total = stat.TotalCount
                      })
                .ToListAsync(ct);

            var allRanks = await topGroup.ToListAsync(ct);

            // Find user who started command
            var me = await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.UserId == myTelegramId, ct);

            int myPosition = 0;
            if (me is not null)
                myPosition = allRanks.FindIndex(x => x.UserRefId == me.Id) + 1;

            // Compile the text
            string title;
            if (isRating)
                title = chatIdFilter.HasValue
                    ? "📊 *Локальный топ-10 по рейтингу*"
                    : "🌐 *Глобальный топ-10 по рейтингу*";
            else
                title = chatIdFilter.HasValue
                    ? "📊 *Локальный топ-10 по сообщениям*"
                    : "🌐 *Глобальный топ-10 по сообщениям*";

            var sb = new StringBuilder()
                .AppendLine(title)
                .AppendLine();

            for (int i = 0; i < top.Count; i++)
            {
                var row = top[i];
                sb.AppendLine($"{i + 1}. [{row.Display}](tg://user?id={row.TelegramId}) — *{row.Total}*");
            }

            sb.AppendLine()
              .AppendLine(myPosition > 0
                  ? $"_Ты на {myPosition}-м месте_"
                  : "_Твоей статистики пока нет_");

            return sb.ToString();
        }

        // Auxiliary DTO so that EF Core knows the structure
        private class StatDto
        {
            public long UserRefId { get; set; }
            public long ChatId { get; set; }
            public int Count { get; set; }
        }
    }
}
