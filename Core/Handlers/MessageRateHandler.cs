using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Infrastructure.Data;
using MyUpdatedBot.Services.UserLeaderboard;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class MessageRateHandler : ICommandHandler
    {
        private readonly IUserLeaderboardService _statsQuery;
        private readonly ILogger<MessageRateHandler> _logger;

        public MessageRateHandler(IUserLeaderboardService statsQuery, ILogger<MessageRateHandler> logger)
        {
            _statsQuery = statsQuery;
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

            var cmd = message.Text!.Trim().ToLowerInvariant();
            bool isLocal = cmd.Contains("local");
            
            var resultText = await _statsQuery.TopTen(
                chatIdFilter: isLocal ? message.Chat.Id : (long?) null,
                isRating: false, // isRating = false - count the number of messages, not rating.
                UserId: message.From!.Id,
                ct);

            _logger.LogInformation(
                "[MessageRateHandler]: Sending message stats to chat {ChatId}", message.Chat.Id);

            await client.SendMessage(
                chatId: message.Chat.Id,
                text: resultText,
                parseMode: ParseMode.Markdown,
                disableNotification: true,
                cancellationToken: ct);
        }
    }

}