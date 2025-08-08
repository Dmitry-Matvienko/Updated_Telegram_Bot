
namespace MyUpdatedBot.Services.MessageStats
{
    /// <summary>
    /// Allows you to record the message has been received and save them in batches to the database.
    /// </summary>
    public interface IMessageCountStatsService
    {
        void EnqueueMessage(long UserId, long ChatId, string? firstName, string? username);
    }
}
