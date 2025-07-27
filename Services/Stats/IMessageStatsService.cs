using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Services.Stats
{
    /// <summary>
    /// Allows you to record the message has been received and save them in batches to the database.
    /// </summary>
    public interface IMessageStatsService
    {
        void EnqueueMessage(long UserId, long ChatId, string? firstName, string? username);
    }
}
