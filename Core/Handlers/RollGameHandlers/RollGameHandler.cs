using MyUpdatedBot.Core.Localization;
using MyUpdatedBot.Services.RollGame;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.RollGameHandlers
{
    public class RollGameHandler : IMessageHandler
    {
        private readonly IRollService _rollService;
        private readonly TimeSpan _duration;
        private readonly LocalizationUtil _loc;

        public RollGameHandler(IRollService rollService, LocalizationUtil loc, TimeSpan? duration = null)
        {
            _rollService = rollService;
            _duration = duration ?? TimeSpan.FromSeconds(60);
            _loc = loc;
        }

        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
            if (string.IsNullOrWhiteSpace(message.Text)) return false;

            return message.Text.StartsWith("/rollgame", StringComparison.OrdinalIgnoreCase);
        }
        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var eventId = _rollService.CreateEvent(chatId, message.From!.Id, _duration);

            var kb = new InlineKeyboardMarkup(new[]
            {
                new [] {InlineKeyboardButton.WithCallbackData(await _loc.GetStringAsync(chatId, message, "RollButton"), $"roll:{eventId:N}") },
                new [] {InlineKeyboardButton.WithCallbackData(await _loc.GetStringAsync(chatId, message, "StopButton"), $"stop:{eventId:N}") }
            });

            var text = $"🎲 {await _loc.GetStringAsync(chatId, message, "StartEvent")}: [{message.From.FirstName}](tg://user?id={message.From.Id})\n\n" +
                       $"{await _loc.GetStringAsync(chatId, message, "RollDice")} {_duration.TotalSeconds}";

            var sent = await botClient.SendMessage(chatId, text, ParseMode.Markdown, replyMarkup: kb, cancellationToken: ct);

            // notify the messageId to service so that it can be edited
            _rollService.SetMessageId(eventId, sent.MessageId);
        }
    }
}
