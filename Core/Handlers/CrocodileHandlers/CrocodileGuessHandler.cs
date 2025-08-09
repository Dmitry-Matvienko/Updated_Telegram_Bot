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
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers.CrocodileHandlers
{
    public class CrocodileGuessHandler : ICommandHandler
    {
        private readonly ICrocodileService _games;

        public CrocodileGuessHandler(ICrocodileService games) => _games = games;
        public bool CanHandle(string text)
        {
            return !string.IsNullOrWhiteSpace(text)
                   && !text.StartsWith("/");
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var text = message.Text ?? "";

            // leave if there is no game going on in this chat room
            if (!_games.HasGame(chatId))
                return;
            // take state and ignoring host
            if (_games.TryGetGameState(chatId, out var state) && state.HostUserId == message.From!.Id)
            {
                return;
            }

            if (_games.TryGuess(chatId, message.From!.Id, text, out var correct) && correct)
            {
                var winnerMention = $"[{message.From.FirstName}](tg://user?id={message.From.Id})";

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"🎉 {winnerMention} угадал(а) слово! Правильный ответ: *{state.CurrentWord}*\n\n" +
                    $"Чтобы начать новую игру, нажми: /crocodile",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct);

                _games.EndGame(chatId);
            }
        }
    }
}
