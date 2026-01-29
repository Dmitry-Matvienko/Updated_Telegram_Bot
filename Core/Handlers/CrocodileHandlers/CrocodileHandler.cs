using MyUpdatedBot.Services.CrocodileGame;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.CrocodileHandlers
{
    public class CrocodileHandler : IMessageHandler
    {
        private readonly ICrocodileService _games;

        public CrocodileHandler(ICrocodileService games) => _games = games;
        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
            if (string.IsNullOrWhiteSpace(message.Text)) return false;

            return message.Text.StartsWith("/crocodile", StringComparison.OrdinalIgnoreCase);
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var userId = message.From!.Id;

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
               {
                    new[]{InlineKeyboardButton.WithCallbackData("Показать слово", "show_word") },
                    new[]{InlineKeyboardButton.WithCallbackData("Изменить слово", "change_word") },
                    new[]{InlineKeyboardButton.WithCallbackData("Завершить игру", "end_game") },
                });
            if (_games.TryStartGame(chatId, userId, out var word))
            {
                await botClient.SendMessage(chatId,
                    $"Игра «Крокодил» начата! Ведущий: [{message.From.FirstName}](tg://user?id={message.From.Id})\n" +
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
                    await botClient.SendMessage(
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
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "⚠️ Не смог узнать состояние игры. Попробуйте позже.",
                        cancellationToken: ct);
                }
            }
        }
    }
}
