using Microsoft.Extensions.Logging;
using MyUpdatedBot.Services.UserLeaderboard;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class TopMessageCountHandler : IMessageHandler
    {
        private readonly IUserLeaderboardService _userLeaderboard;
        private readonly ILogger<TopMessageCountHandler> _logger;

        public TopMessageCountHandler(IUserLeaderboardService userLeaderboard, ILogger<TopMessageCountHandler> logger)
        {
            _userLeaderboard = userLeaderboard;
            _logger = logger;
        }

        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
            if (string.IsNullOrWhiteSpace(message.Text)) return false;

            return message.Text.StartsWith("/GlobalMessage", StringComparison.OrdinalIgnoreCase)
                || message.Text.StartsWith("/LocalMessage", StringComparison.OrdinalIgnoreCase);
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {

            var cmd = message.Text!.Trim().ToLowerInvariant();
            bool isLocal = cmd.Contains("local");
            
            var resultText = await _userLeaderboard.TopTen(
                chatIdFilter: isLocal ? message.Chat.Id : (long?) null,
                isRating: false, // if isRating = false - count the number of messages, not reputation
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