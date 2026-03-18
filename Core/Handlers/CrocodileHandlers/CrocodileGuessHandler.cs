using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Localization;
using MyUpdatedBot.Services.CrocodileGame;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers.CrocodileHandlers
{
    public class CrocodileGuessHandler : IMessageHandler
    {
        private readonly ICrocodileService _games;
        private readonly ILogger<CrocodileGuessHandler> _logger;
        private readonly LocalizationUtil _loc;

        public CrocodileGuessHandler(ICrocodileService games, ILogger<CrocodileGuessHandler> logger, LocalizationUtil loc)
        {
            _games = games;
            _logger = logger;
            _loc = loc;
        }
        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
            if (string.IsNullOrWhiteSpace(message.Text)) return false;

            // leave if there is no game going on in this chat room
            if (!_games.HasGame(message.Chat.Id)) return false;
            // take state and ignoring host
            if (_games.TryGetGameState(message.Chat.Id, out var state) && state is not null && state.HostUserId == message.From.Id)
                return false;

            return true;
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var userId = message.From!.Id;
            var text = message.Text!;

            if (!_games.TryGetGameState(chatId, out var state) || state is null)
                return;

            try
            {
                if (_games.TryGuess(chatId, userId, text, out var correct) && correct)
                {
                    var winnerMention = $"[{message.From.FirstName}](tg://user?id={message.From.Id})";

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"🎉 {winnerMention} {await _loc.GetStringAsync(chatId, message, "GuessedWord")} *{state?.CurrentWord ?? "—"}*\n\n" +
                              $"{await _loc.GetStringAsync(chatId, message, "ToStartNewGame")} /crocodile",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct);

                    _games.EndGame(chatId);

                    _logger.LogInformation("[CrocodileGuessHandler]: user {UserId} won in chat {ChatId}", userId, chatId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CrocodileGuessHandler]: failed while processing guess in chat {ChatId} by user {UserId}", chatId, userId);
            }
        }
    }
}
