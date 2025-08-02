using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Services.UserLeaderboard
{
    public interface IUserLeaderboardService
    {
        Task<string> TopTen(long? chatIdFilter, bool isRating, long UserId, CancellationToken ct);
    }
}
