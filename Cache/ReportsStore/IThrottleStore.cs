
namespace MyUpdatedBot.Cache
{
    public interface IThrottleStore
    {
        bool TryCheckAndSet((long chat, long user) key, TimeSpan throttleDelay, out int waitSeconds);
    }
}
