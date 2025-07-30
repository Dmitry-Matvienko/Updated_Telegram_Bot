using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Infrastructure.Data;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class MessageRateHandler : ICommandHandler
    {
        private readonly MyDbContext _db;
        private readonly ILogger<MessageRateHandler> _logger;

        public MessageRateHandler(MyDbContext db, ILogger<MessageRateHandler> logger)
        {
            _db = db;
            _logger = logger;
        }

        public bool CanHandle(string text)
        {
            text = text?.Trim() ?? "";
            return text.StartsWith("/GlobalMessage", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/LocalMessage", StringComparison.OrdinalIgnoreCase);
        }

        public async Task HandleAsync(ITelegramBotClient client, Message message, CancellationToken ct)
        {
            _logger.LogInformation("[MessageRateHandler]: User: {FirstName} {UserId} invoked {Command} in chat {ChatId}",
                message.From?.FirstName,
                message.From!.Id,
                message.Text,
                message.Chat.Id);

            // Determine what rating is needed
            var cmd = message.Text!.Trim();
            bool isLocal = cmd.StartsWith("/LocalMessage", StringComparison.OrdinalIgnoreCase);
            bool isGlobal = cmd.StartsWith("/GlobalMessage", StringComparison.OrdinalIgnoreCase);

            long? chatFilter = isLocal ? message.Chat.Id : (long?) null;

            // 1) Take top‑10
            async Task<(long TelegramId, string Display, int Total)[]> GetTop10Async()
            {
                var baseQuery = _db.MessageStats
                    .AsNoTracking()
                    .Where(ms => chatFilter == null || ms.ChatId == chatFilter.Value);

                var top = await baseQuery
                    .GroupBy(ms => ms.UserRefId)
                    .Select(g => new {
                        UserRefId = g.Key,
                        TotalCount = g.Sum(ms => ms.MessageCount)
                    })
                    .OrderByDescending(x => x.TotalCount)
                    .Take(10)
                    .Join(_db.Users,
                          stat => stat.UserRefId,
                          u => u.Id,
                          (stat, u) => new {
                              TelegramId = u.UserId,
                              Display = u.FirstName ?? u.Username ?? "–",
                              Total = stat.TotalCount
                          })
                    .ToArrayAsync(ct);

                return top.Select(x => (x.TelegramId, x.Display, x.Total)).ToArray();
            }

            var top10 = await GetTop10Async();

            _logger.LogDebug("[MessageRateHandler]: Retrieved {Count} top records (Local={IsLocal})", top10.Length, isLocal);

            // Perform a full aggregation to find user position 
            // (without Take, but only for the required chat or for all chats).
            var allRanks = await _db.MessageStats
                .AsNoTracking()
                .Where(ms => chatFilter == null || ms.ChatId == chatFilter.Value)
                .GroupBy(ms => ms.UserRefId)
                .Select(g => new {
                    UserRefId = g.Key,
                    TotalCount = g.Sum(ms => ms.MessageCount)
                })
                .OrderByDescending(x => x.TotalCount)
                .ToListAsync(ct);

            // Find the internal PK of the current user
            var me = await _db.Users
                .AsNoTracking()
                .SingleAsync(u => u.UserId == message.From!.Id, ct);

            // Determine the index and +1 if not found — 0
            int myPosition = allRanks
                .FindIndex(x => x.UserRefId == me.Id) + 1;

            // Composing a message
            var title = isLocal
                ? $"📊 *Локальный топ‑10 болтунов в этом чате*"
                : $"🌐 *Глобальный топ‑10 болтунов*";

            var sb = new StringBuilder()
                .AppendLine(title)
                .AppendLine();

            for (int i = 0; i < top10.Length; i++)
            {
                var (tgId, name, total) = top10[i];
                sb.AppendLine($"{i + 1}. [{name}](tg://user?id={tgId}) — *{total}*✉️");
            }

            // Always show user's position even if it's higher than top-10
            sb
              .AppendLine()
              .AppendLine(myPosition > 0
                  ? $"_Ты на {myPosition}-м месте_"
                  : "_Твоей статистики пока нет_");

            _logger.LogInformation("[MessageRateHandler]: Sending rating message to chat {ChatId}", message.Chat.Id);

            await client.SendMessage(
                chatId: message.Chat.Id,
                text: sb.ToString(),
                parseMode: ParseMode.Markdown,
                disableNotification: true,
                cancellationToken: ct);
        }
    }

}