using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
