
namespace MyUpdatedBot.Services.OwnerTools
{
    public interface IBroadcastService
    {
        Task BroadcastAsync(string text, CancellationToken ct);
    }
}
