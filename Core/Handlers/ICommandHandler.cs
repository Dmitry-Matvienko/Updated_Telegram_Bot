using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public interface ICommandHandler
    {
        bool CanHandle(string text);
        Task HandleAsync(ITelegramBotClient client, Message message, CancellationToken ct);
    }
}
