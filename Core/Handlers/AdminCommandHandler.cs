using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyUpdatedBot.Core.Models;
using MyUpdatedBot.Services.AdminPanel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Handlers
{
    public class AdminCommandHandler : ICommandHandler
    {
        private readonly IBroadcastService _bcast;
        private readonly IAdminStatsService _stats;
        private readonly IReadOnlyCollection<long> _admins;

        public AdminCommandHandler(IBroadcastService bcast,IAdminStatsService stats, IOptions<AdminSettings> admins)
        {
            _bcast = bcast;
            _stats = stats;
            _admins = admins.Value.Ids;
        }

        public bool CanHandle(string text) => text.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);

        public async Task HandleAsync(ITelegramBotClient bot, Message msg, CancellationToken ct)
        {
            if (!_admins.Contains(msg.From!.Id))
                return; // ignoring if not admin

            var parts = msg.Text!.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                await bot.SendMessage(msg.Chat.Id,
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
                await bot.SendMessage(msg.Chat.Id, $"Всего пользователей: {total}", cancellationToken: ct);
            }
            else if (cmd.StartsWith("broadcast ", StringComparison.OrdinalIgnoreCase))
            {
                var text = cmd.Substring("broadcast ".Length);
                await _bcast.BroadcastAsync(text, ct);
                await bot.SendMessage(msg.Chat.Id, "Рассылка отправлена.", cancellationToken: ct);
            }
            else
            {
                await bot.SendMessage(msg.Chat.Id, "Неизвестная подкоманда.", cancellationToken: ct);
            }
        }
    }
}
