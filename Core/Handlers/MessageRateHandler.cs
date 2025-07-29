using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Handlers;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Infrastructure.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class MessageRateHandler : ICommandHandler
    {
        public async Task HandleAsync(ITelegramBotClient client, Message message, CancellationToken ct)
        {
            // TODO: implement logic for message rating
            throw new NotImplementedException();
        }
    }

}