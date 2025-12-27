using MyUpdatedBot.Services.MessageStats;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public class CountMessageHandler : IMessageHandler
    {
        private readonly IMessageCountStatsService _messageCount;

        public CountMessageHandler(IMessageCountStatsService messageCount)
        {
            _messageCount = messageCount;
        }

        public bool CanHandle(Message? message)
        {
            if (message is null) return false;
            return message.From != null && message.Chat != null;
        } // Сapture all messages from user

        public Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            if (message.From is null) return Task.CompletedTask;

            _messageCount.EnqueueMessage(message.From!.Id, message.Chat.Id, message.From.FirstName, message.From.Username);
            return Task.CompletedTask;
        }
    }
}
