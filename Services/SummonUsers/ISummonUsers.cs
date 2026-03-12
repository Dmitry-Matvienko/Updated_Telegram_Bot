namespace MyUpdatedBot.Services.SummonUsers
{
    public interface ISummonUsers
    {
        Task<IReadOnlyList<(long TelegramId, string DisplayName)>> GetUsersAsync(long chatId, int limit, CancellationToken ct = default);
    }
}
