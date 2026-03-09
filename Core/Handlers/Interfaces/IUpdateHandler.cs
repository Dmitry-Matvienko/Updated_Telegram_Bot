using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public interface IUpdateHandlerService
    {
        Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct);
    }
}
