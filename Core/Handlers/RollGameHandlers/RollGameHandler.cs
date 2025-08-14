using MyUpdatedBot.Services.RollGame;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MyUpdatedBot.Core.Handlers.RollGameHandlers
{
    internal class RollGameHandler : ICommandHandler
    {
        private readonly IRollService _rollService;

        public RollGameHandler(IRollService rollService) 
        {
            _rollService = rollService;
        }

        public bool CanHandle(string text) => text.StartsWith("/roll_game");
        public async Task HandleAsync(ITelegramBotClient client, Message message, CancellationToken ct)
        {
            var duration = TimeSpan.FromMinutes(1);
            var eventId = _rollService.CreateEvent(message.Chat.Id, message.From!.Id, duration);

            var kb = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("Ролл 🎲", $"roll:{eventId:N}"),
                InlineKeyboardButton.WithCallbackData("Остановить розыгрыш ⛔️", $"stop:{eventId:N}")
            });

            var text = $"🎲 Розыгрыш начат! Инициатор: [{message.From.FirstName}](tg://user?id={message.From.Id})\n\n" +
                       $"Нажмите «Ролл 🎲» чтобы бросить.\nВремя: {duration.TotalMinutes} минуты";

            var sent = await client.SendMessage(message.Chat.Id, text, ParseMode.Markdown, replyMarkup: kb, cancellationToken: ct);

            // notify the messageId to service so that it can be edited
            _rollService.SetMessageId(eventId, sent.MessageId);
        }
    }
}
