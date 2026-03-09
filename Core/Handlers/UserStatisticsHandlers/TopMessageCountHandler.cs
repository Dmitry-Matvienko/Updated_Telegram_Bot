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
    public class TopMessageCountHandler : ICommandHandler
    {
        private readonly IUserLeaderboardService _userLeaderboard;
        private readonly ILogger<TopMessageCountHandler> _logger;

        public TopMessageCountHandler(IUserLeaderboardService userLeaderboard, ILogger<TopMessageCountHandler> logger)
        {
            _userLeaderboard = userLeaderboard;
            _logger = logger;
        }

        public bool CanHandle(string text)
        {
            text = text?.Trim() ?? "";
            return text.StartsWith("/GlobalMessage", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/LocalMessage", StringComparison.OrdinalIgnoreCase);
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {

            var cmd = message.Text!.Trim().ToLowerInvariant();
            bool isLocal = cmd.Contains("local");
            
            var resultText = await _userLeaderboard.TopTen(
                chatIdFilter: isLocal ? message.Chat.Id : (long?) null,
                isRating: false, // isRating = false - count the number of messages, not rating.
                UserId: message.From!.Id,
                ct);

            _logger.LogInformation(
                "[MessageRateHandler]: Sending message stats to chat {ChatId}", message.Chat.Id);

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: resultText,
                parseMode: ParseMode.Markdown,
                disableNotification: true,
                cancellationToken: ct);
        }
    }

}