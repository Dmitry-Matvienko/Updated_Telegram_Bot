using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyUpdatedBot.Core.Models;
using MyUpdatedBot.Services.OwnerTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public class OwnerCommandHandler : ICommandHandler
    {
        private readonly IBroadcastService _bcast;
        private readonly IUserStatsService _stats;
        private readonly IReadOnlyCollection<long> _admins;

        public OwnerCommandHandler(IBroadcastService bcast,IUserStatsService stats, IOptions<OwnerSettings> admins)
        {
            _bcast = bcast;
            _stats = stats;
            _admins = admins.Value.Ids;
        }

        public bool CanHandle(string text) => text.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            if (!_admins.Contains(message.From!.Id))
                return; // ignoring if not admin

            var parts = message.Text!.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                await botClient.SendMessage(message.Chat.Id,
                    "Доступные команды:\n" +
                    "/admin userscount - количество пользователй\n" +
                    "/admin broadcast [Текст] - рассылка всем пользователям\n",
                    cancellationToken: ct);
                return;
            }

            var cmd = parts[1];
            if (cmd.StartsWith("userscount", StringComparison.OrdinalIgnoreCase))
            {
                var total = await _stats.GetTotalUsersAsync(ct);
                await botClient.SendMessage(message.Chat.Id, $"Всего пользователей: {total}", cancellationToken: ct);
            }
            else if (cmd.StartsWith("broadcast ", StringComparison.OrdinalIgnoreCase))
            {
                var text = cmd.Substring("broadcast ".Length);
                await _bcast.BroadcastAsync(text, ct);
                await botClient.SendMessage(message.Chat.Id, "Рассылка отправлена.", cancellationToken: ct);
            }
            else
            {
                await botClient.SendMessage(message.Chat.Id, "Неизвестная подкоманда.", cancellationToken: ct);
            }
        }
    }
}
