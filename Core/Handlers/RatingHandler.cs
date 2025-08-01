using MyUpdatedBot.Infrastructure.Data;
using MyUpdatedBot.Services.Rating;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class RatingHandler : ICommandHandler
    {
        private readonly IRatingService _ratingService;

        public RatingHandler(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        public bool CanHandle(string text)
        {
            return text.StartsWith("Спасибо", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Благодарю", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/localrating", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/globalrating", StringComparison.OrdinalIgnoreCase);
        }

        public async Task HandleAsync(ITelegramBotClient client, Message message, CancellationToken ct)
        {
            var text = message.Text?.Trim() ?? "";

            //if (text.Equals("/globalrating", StringComparison.OrdinalIgnoreCase) || text.Equals("/localrating", StringComparison.OrdinalIgnoreCase))
            //{
            //    var top10 = await _ratingService.TopLocalRate(message.Chat.Id, message.From.Id, message.Text, ct);
            //    await client.SendMessage(message.Chat.Id, $"{top10}", parseMode: ParseMode.Markdown, cancellationToken: ct);
            //}

            if (message.ReplyToMessage is null) return;
            var txt = message.Text?.ToLowerInvariant() ?? "";
            if ((!txt.StartsWith("спасибо") && !txt.StartsWith("благодарю"))
                || message.From?.Id == message.ReplyToMessage.From?.Id)
                return;

            var given = await _ratingService.GiveRatingAsync(
                fromUserId: message.From!.Id,
                toUserId: message.ReplyToMessage.From!.Id,
                chatId: message.Chat.Id,
                ct);

            if (given)
                await client.SendMessage(message.Chat.Id, $"[{message.ReplyToMessage.From.FirstName}](tg://user?id={message.From.Id}) ты получил(а) рейтинг", parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
    }
}
