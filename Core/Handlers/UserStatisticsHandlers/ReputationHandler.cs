using Microsoft.Extensions.Logging;
using MyUpdatedBot.Services.UserReputation;
using MyUpdatedBot.Services.UserLeaderboard;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class ReputationHandler : IMessageHandler
    {
        private readonly IReputationService _reputationService;
        private readonly ILogger<ReputationHandler> _logger;
        private readonly IUserLeaderboardService _userLeaderboard;

        public ReputationHandler(IReputationService reputationService, ILogger<ReputationHandler> logger, IUserLeaderboardService userLeaderboard)
        {
            _reputationService = reputationService;
            _logger = logger;
            _userLeaderboard = userLeaderboard;
        }

        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
            if (string.IsNullOrWhiteSpace(message.Text)) return false;

            return message.Text.StartsWith("Спасибо", StringComparison.OrdinalIgnoreCase)
                || message.Text.StartsWith("Благодарю", StringComparison.OrdinalIgnoreCase)
                || message.Text.StartsWith("/localrating", StringComparison.OrdinalIgnoreCase)
                || message.Text.StartsWith("/globalrating", StringComparison.OrdinalIgnoreCase);
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var text = message.Text?.Trim() ?? "";

            if (text.StartsWith("/localrating", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/globalrating", StringComparison.OrdinalIgnoreCase))
            {
                bool isLocal = text.StartsWith("/localrating", StringComparison.OrdinalIgnoreCase);

                _logger.LogInformation(
                    "User {Id} invoked {Cmd} in chat {Chat}",
                    message.From!.Id, message.Text, message.Chat.Id);

                var resultText = await _userLeaderboard.TopTen(
                    chatIdFilter: isLocal ? message.Chat.Id : (long?) null,
                    isRating: true, // isRating = true - count the number of rating, not messages.
                    UserId: message.From.Id,
                    ct);

                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: resultText,
                    parseMode: ParseMode.Markdown,
                    disableNotification: true,
                    cancellationToken: ct);
            }

            if (message.ReplyToMessage is null) return;
            var txt = message.Text?.ToLowerInvariant() ?? "";
            if ((!txt.StartsWith("спасибо") && !txt.StartsWith("благодарю"))
                || message.From?.Id == message.ReplyToMessage.From?.Id)
                return;

            var given = await _reputationService.GiveReputationAsync(
                fromUserId: message.From!.Id,
                toUserId: message.ReplyToMessage.From!.Id,
                chatId: message.Chat.Id,
                ct);

            if (given)
                await botClient.SendMessage(message.Chat.Id, $"[{message.ReplyToMessage.From.FirstName}](tg://user?id={message.ReplyToMessage.From.Id}) ты получил(а) +1 рейтинг", parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
    }
}
