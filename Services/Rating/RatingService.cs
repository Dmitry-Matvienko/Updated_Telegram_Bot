using Microsoft.EntityFrameworkCore;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Services.Rating
{
    public class RatingService : IRatingService
    {
        private readonly MyDbContext _db;
        private readonly ITelegramBotClient _botClient;

        public RatingService(MyDbContext db, ITelegramBotClient botClient)
        {
            _db = db;
            _botClient = botClient;
        }

        public async Task<bool> GiveRatingAsync(long fromUserId, long toUserId, long chatId, CancellationToken ct)
        {
            if (fromUserId == toUserId)
                return false; // Can't give yourself

            var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == toUserId, ct);
            if (user is null)
                return false; // not registered

            // For find or create RatingEntity
            var rating = await _db.RatingStats
                .SingleOrDefaultAsync(r => r.UserRefId == user.Id && r.ChatId == chatId, ct);

            // Check the timer
            var now = DateTime.UtcNow;
            if (rating != null && (now - rating.LastGiven).TotalMinutes < 3)
                return false;

            if (rating is null)
            {
                rating = new RatingEntity
                {
                    UserRefId = user.Id,
                    ChatId = chatId,
                    Rating = 1,
                    LastGiven = now
                };
                _db.RatingStats.Add(rating);
            }
            else
            {
                rating.Rating += 1;
                rating.LastGiven = now;
            }

            await _db.SaveChangesAsync(ct);
            return true;
        }

    }

}
