using MyUpdatedBot.Services.RollGame;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.RollGameHandlers
{
    internal class RollGameHandler : IMessageHandler
    {
        private readonly IRollService _rollService;
        private readonly TimeSpan _duration;

        public RollGameHandler(IRollService rollService, TimeSpan? duration = null) 
        {
            _rollService = rollService;
            _duration = duration ?? TimeSpan.FromMinutes(1);
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
            var eventId = _rollService.CreateEvent(message.Chat.Id, message.From!.Id, _duration);

            var kb = new InlineKeyboardMarkup(new[]
            {
                new [] {InlineKeyboardButton.WithCallbackData("Ролл 🎲", $"roll:{eventId:N}") },
                new [] {InlineKeyboardButton.WithCallbackData("Остановить ⛔️", $"stop:{eventId:N}") }
            });

            var text = $"🎲 Розыгрыш начат! Инициатор: [{message.From.FirstName}](tg://user?id={message.From.Id})\n\n" +
                       $"Нажмите «Ролл 🎲» чтобы бросить.\nВремя: {_duration.TotalMinutes} минуты";

            var sent = await botClient.SendMessage(message.Chat.Id, text, ParseMode.Markdown, replyMarkup: kb, cancellationToken: ct);

            // notify the messageId to service so that it can be edited
            _rollService.SetMessageId(eventId, sent.MessageId);
        }
    }
}
