
namespace MyUpdatedBot.Services.UserReputation
{
    public interface IReputationService
    {
        Task<bool> GiveReputationAsync(long fromUserId, long toUserId, long chatId, CancellationToken ct);
    }
}
