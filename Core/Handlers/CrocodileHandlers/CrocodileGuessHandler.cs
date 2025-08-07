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

        public async Task HandleAsync(ITelegramBotClient bot, Message msg, CancellationToken ct)
        {
            var chatId = msg.Chat.Id;
            var text = msg.Text ?? "";

            // leave if there is no game going on in this chat room
            if (!_games.HasGame(chatId))
                return;
            // take state and ignoring host
            if (_games.TryGetGameState(chatId, out var state) && state.HostUserId == msg.From!.Id)
            {
                return;
            }

            if (_games.TryGuess(chatId, msg.From!.Id, text, out var correct) && correct)
            {
                var winnerMention = $"[{msg.From.FirstName}](tg://user?id={msg.From.Id})";

                await bot.SendMessage(
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
