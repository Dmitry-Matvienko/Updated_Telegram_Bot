using Microsoft.EntityFrameworkCore;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Infrastructure.Data;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Threading;

namespace MyUpdatedBot.Core.Handlers
{
    public class OptionalHandler : ICommandHandler
    {
        private readonly ITelegramBotClient _client;

        public OptionalHandler(MyDbContext db, ITelegramBotClient client)
        {
            _client = client;
        }

        public bool CanHandle(string text) => text.StartsWith("/start");

        public async Task HandleAsync(ITelegramBotClient client, Message message, CancellationToken ct)
        {

        }
    }
}
