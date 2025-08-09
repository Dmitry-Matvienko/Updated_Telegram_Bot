using MyUpdatedBot.Services.MessageStats;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public class CountTextMessageHandler : ICommandHandler
    {
        private readonly IMessageCountStatsService _messageCount;

        public CountTextMessageHandler(IMessageCountStatsService messageCount)
        {
            _messageCount = messageCount;
        }

        public bool CanHandle(string text) => true; // Сapture all text messages

        public Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            _messageCount.EnqueueMessage(message.From!.Id, message.Chat.Id, message.From.FirstName, message.From.Username);
            return Task.CompletedTask;
        }
    }
}
