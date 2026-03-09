using MyUpdatedBot.Services.MessageStats;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class MessageCountHandler : IMessageHandler
    {
        private readonly IMessageCountStatsService _messageCount;

        public MessageCountHandler(IMessageCountStatsService messageCount)
        {
            _messageCount = messageCount;
        }

        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
            if (string.IsNullOrWhiteSpace(message.Text)) return false;

            return true;
        } // Сapture all messages from user

        public Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            if (message.From is null) return Task.CompletedTask;

            _messageCount.EnqueueMessage(message.From!.Id, message.Chat.Id, message.From.FirstName, message.From.Username);
            return Task.CompletedTask;
        }
    }
}
