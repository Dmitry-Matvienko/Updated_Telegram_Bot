using Microsoft.Extensions.Logging;
using MyUpdatedBot.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Microsoft.EntityFrameworkCore;

namespace MyUpdatedBot.Services.AdminPanel
{
    public class BroadcastService : IBroadcastService
    {
        private readonly MyDbContext _db;
        private readonly ITelegramBotClient _bot;
        private readonly ILogger<BroadcastService> _log;

        public BroadcastService(MyDbContext db, ITelegramBotClient bot, ILogger<BroadcastService> log)
        {
            _db = db;
            _bot = bot;
            _log = log;
        }

        public async Task BroadcastAsync(string text, CancellationToken ct)
        {
            var allChats = await _db.Users
                .AsNoTracking()
                .Select(u => u.UserId)
                .ToListAsync(ct);

            foreach (var chatId in allChats)
            {
                try
                {
                    await _bot.SendMessage(chatId, text, cancellationToken: ct);
                    _log.LogInformation("[BroadcastService]: сообщение отправлено в чат: {Chat}", chatId);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "[BroadcastService]: Не удалось отправить broadcast в {Chat}", chatId);
                }
            }
        }
    }
}
