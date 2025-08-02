using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Services.Rating
{
    public interface IRatingService
    {
        Task<bool> GiveRatingAsync(long fromUserId, long toUserId, long chatId, CancellationToken ct);
    }
}
