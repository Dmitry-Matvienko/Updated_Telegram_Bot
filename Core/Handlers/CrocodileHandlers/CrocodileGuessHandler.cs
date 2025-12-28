using Microsoft.Extensions.Logging;
using Mono.TextTemplating;
using MyUpdatedBot.Services.CrocodileGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.CrocodileHandlers
{
    public class CrocodileGuessHandler : IMessageHandler
    {
        private readonly ICrocodileService _games;
        private readonly ILogger<CrocodileGuessHandler> _logger;

        public CrocodileGuessHandler(ICrocodileService games, ILogger<CrocodileGuessHandler> logger)
        {
            _games = games;
            _logger = logger;
        }
        public bool CanHandle(Message? message)
        {
            if (message is null) return false;

            var from = message.From;
            if (from is null) return false;

            var chat = message.Chat;
            if (chat is null) return false;
            var chatId = chat.Id;

            var text = message.Text;
            if (string.IsNullOrWhiteSpace(text)) return false;

            if (text.StartsWith("/")) return false;
            // leave if there is no game going on in this chat room
            if (!_games.HasGame(chatId)) return false;
            // take state and ignoring host
            if (_games.TryGetGameState(chatId, out var state) && state is not null && state.HostUserId == from.Id)
                return false;

            return true;
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            if (message.From is null || message.Chat is null || string.IsNullOrWhiteSpace(message.Text))
                return;

            var chatId = message.Chat.Id;
            var userId = message.From.Id;
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
                        text: $"🎉 {winnerMention} угадал(а) слово! Правильный ответ: *{state?.CurrentWord ?? "—"}*\n\n" +
                              $"Чтобы начать новую игру, нажми: /crocodile",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct);

                    _games.EndGame(chatId);

                    _logger.LogInformation("Crocodile: user {UserId} won in chat {ChatId}", userId, chatId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CrocodileGuessHandler failed while processing guess in chat {ChatId} by user {UserId}", chatId, userId);
            }
        }
    }
}
