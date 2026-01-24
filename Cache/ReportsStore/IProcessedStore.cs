namespace MyUpdatedBot.Cache.ReportsStore
{
    public record ProcessedInfo(string Action, long AdminId, string AdminName, DateTime When);
    public interface IProcessedStore
    {
        bool TryGet((long sourceChat, int sourceMessageId, long targetUser) key, out ProcessedInfo info);
        bool TryAdd((long sourceChat, int sourceMessageId, long targetUser) key, ProcessedInfo info);
        bool TryRemove((long sourceChat, int sourceMessageId, long targetUser) key, out ProcessedInfo info);
    }
}
