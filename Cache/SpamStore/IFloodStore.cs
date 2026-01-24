namespace MyUpdatedBot.Cache.SpamStore
{
    public interface IFloodStore
    {
        /// <summary>
        /// returns true if it is spam (threshold reached)
        /// </summary>
        Task<bool> AddAndCheckAsync(long chatId, long userId);
        void SetCachedWarningsCount(long chatId, long userId, int count);
    }
}
