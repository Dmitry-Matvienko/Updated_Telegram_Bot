
namespace MyUpdatedBot.Cache.ReportsStore
{
    public interface IThrottleStore
    {
        bool TryCheckAndSet((long chat, long user) key, out int waitSeconds);
    }
}
