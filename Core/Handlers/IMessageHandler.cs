using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
