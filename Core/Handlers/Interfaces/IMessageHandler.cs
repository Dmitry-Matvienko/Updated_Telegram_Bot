using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public interface IMessageHandler
    {
        bool CanHandle(Message? message);
        Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
    }
}
