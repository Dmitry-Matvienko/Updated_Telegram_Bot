using MyUpdatedBot.Core.Models;

namespace MyUpdatedBot.Services.RollGame
{
    public interface IRollService
    {
        Guid CreateEvent(long chatId, long hostUserId, TimeSpan duration);
        // sets MessageId (after the message is sent by the handler)
        void SetMessageId(Guid eventId, int messageId);
        bool TryGetEvent(Guid eventId, out RollGameState? state);
        // throw attempt. if first time - add and return (ok, value, firstTime)
        (bool Ok, int Value, bool FirstTime) TryRoll(Guid eventId, long userId, string? firstName);
        bool StopEvent(Guid eventId);
    }
}
