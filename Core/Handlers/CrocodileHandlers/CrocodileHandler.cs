using Mono.TextTemplating;
using MyUpdatedBot.Services.CrocodileGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.CrocodileHandlers
{
    public class CrocodileHandler : ICommandHandler
    {
        private readonly ICrocodileService _games;
        public CrocodileHandler(ICrocodileService games) => _games = games;

        public bool CanHandle(string text) => text.StartsWith("/crocodile", StringComparison.OrdinalIgnoreCase);

        public async Task HandleAsync(ITelegramBotClient bot, Message msg, CancellationToken ct)
        {
            var chatId = msg.Chat.Id;
            var userId = msg.From!.Id;

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
               {
                    new[]{InlineKeyboardButton.WithCallbackData("Показать слово", "show_word") },
                    new[]{InlineKeyboardButton.WithCallbackData("Изменить слово", "change_word") },
                    new[]{ InlineKeyboardButton.WithCallbackData("Завершить игру", "end_game") },
                });
            if (_games.TryStartGame(chatId, userId, out var word))
            {
                await bot.SendMessage(chatId,
                    $"Игра «Крокодил» начата! Ведущий: [{msg.From.FirstName}](tg://user?id={msg.From.Id})\n" +
                    $"Нажми кнопку «Показать слово»\n\n" +
                    $"На раунд даётся *15* минут",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: inlineKeyboard,
                    cancellationToken: ct);
            }
            else
            {
                // warning if the game is already started
                if (_games.TryGetGameState(chatId, out var state))
                {
                    await bot.SendMessage(
                        chatId: chatId,
                        text:
                          $"⚠️ Игра уже идёт в этом чате.\n" +
                          $"[Ведущий игры](tg://user?id={state.HostUserId})",
                        parseMode: ParseMode.Markdown,
                        replyMarkup: inlineKeyboard,
                        cancellationToken: ct);
                }
                else
                {
                    // if an unpredictable error occurs
                    await bot.SendMessage(
                        chatId: chatId,
                        text: "⚠️ Не смог узнать состояние игры. Попробуйте позже.",
                        cancellationToken: ct);
                }
            }
        }
    }
}
