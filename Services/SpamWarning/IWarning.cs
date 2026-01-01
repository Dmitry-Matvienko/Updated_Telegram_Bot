
namespace MyUpdatedBot.Services.SpamProtection
{
    public interface IWarning
    {
        Task<int> AddWarningAsync(long chatId, long telegramUserId, CancellationToken ct = default);
    }
}
