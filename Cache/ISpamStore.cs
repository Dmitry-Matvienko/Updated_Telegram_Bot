
namespace MyUpdatedBot.Cache
{
    public interface ISpamStore
    {
        /// <summary>
        /// returns true if it is spam (threshold reached)
        /// </summary>
        Task<bool> AddAndCheckAsync(long chatId, long userId, CancellationToken ct = default);
        void SetCachedWarningsCount(long chatId, long userId, int count);
        int? TryGetCachedWarningsCount(long chatId, long userId);
    }
}
