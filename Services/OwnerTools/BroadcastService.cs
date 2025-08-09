using Microsoft.Extensions.Logging;
using MyUpdatedBot.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Microsoft.EntityFrameworkCore;

namespace MyUpdatedBot.Services.OwnerTools
{
    public class BroadcastService : IBroadcastService
    {
        private readonly MyDbContext _db;
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<BroadcastService> _logger;

        public BroadcastService(MyDbContext db, ITelegramBotClient botClient, ILogger<BroadcastService> logger)
        {
            _db = db;
            _botClient = botClient;
            _logger = logger;
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
                    await _botClient.SendMessage(chatId, text, cancellationToken: ct);
                    _logger.LogInformation("[BroadcastService]: сообщение отправлено в чат: {Chat}", chatId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[BroadcastService]: Не удалось отправить broadcast в {Chat}", chatId);
                }
            }
        }
    }
}
