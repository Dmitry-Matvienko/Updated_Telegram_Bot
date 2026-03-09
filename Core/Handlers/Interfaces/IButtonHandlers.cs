using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public interface IButtonHandlers
    {
        bool CanHandle(CallbackQuery callbackQuery);
        Task HandleAsync(ITelegramBotClient client, CallbackQuery callbackQuery, CancellationToken ct);
    }
}
