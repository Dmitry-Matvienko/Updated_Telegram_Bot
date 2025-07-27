using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Infrastructure.Data;
using MyUpdatedBot.Services.Stats;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public class CountMessageHandler : ICommandHandler
    {
        private readonly IMessageStatsService _stats;

        public CountMessageHandler(IMessageStatsService stats)
        {
            _stats = stats;
        }

        public bool CanHandle(string text) => true; // Сapture all text messages

        public Task HandleAsync(ITelegramBotClient client, Message message, CancellationToken ct)
        {
            _stats.EnqueueMessage(message.From!.Id, message.Chat.Id, message.From.FirstName, message.From.Username);
            return Task.CompletedTask;
        }
    }
}
