
namespace MyUpdatedBot.Cache.ReportsStore
{
    public interface IUserReportsThrottleStore
    {
        bool TryCheckAndSet((long chat, long user) key, out int waitSeconds);
    }
}
