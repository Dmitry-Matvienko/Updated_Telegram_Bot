using MyUpdatedBot.Core.Localization;
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
        private readonly LocalizationUtil _loc;

        public CrocodileHandler(ICrocodileService games, LocalizationUtil loc)
        {
            _games = games;
            _loc = loc;
        }
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
                    new[]{InlineKeyboardButton.WithCallbackData($"{await _loc.GetStringAsync(chatId, message, "ShowWordButton")}", "show_word") },
                    new[]{InlineKeyboardButton.WithCallbackData($"{await _loc.GetStringAsync(chatId, message, "ChangeWordButton")}", "change_word") },
                    new[]{InlineKeyboardButton.WithCallbackData($"{await _loc.GetStringAsync(chatId, message, "EndGameButton")}", "end_game") },
                });
            if (_games.TryStartGame(chatId, userId, out var word))
            {
                await botClient.SendMessage(chatId,
                    $"{await _loc.GetStringAsync(chatId, message, "StartTheGamePart1")}[{message.From.FirstName}](tg://user?id={message.From.Id})\n" +
                    $"{await _loc.GetStringAsync(chatId, message, "StartTheGamePart2")}",
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
                          $"{await _loc.GetStringAsync(chatId, message, "GameAlreadyStarted")}\n" +
                          $"[{await _loc.GetStringAsync(chatId, message, "HostIs")}](tg://user?id={state.HostUserId})",
                        parseMode: ParseMode.Markdown,
                        replyMarkup: inlineKeyboard,
                        cancellationToken: ct);
                }
                else
                {
                    // if an unpredictable error occurs
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"{await _loc.GetStringAsync(chatId, message, "Error_CrocodileState")}",
                        cancellationToken: ct);
                }
            }
        }
    }
}
