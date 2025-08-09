
namespace MyUpdatedBot.Services.OwnerTools
{
    public interface IUserStatsService
    {
        Task<int> GetTotalUsersAsync(CancellationToken ct);
    }
}
