using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Models;
using System.Collections.Concurrent;

namespace MyUpdatedBot.Services.RollGame
{
    public class RollService : IRollService
    {
        private readonly ConcurrentDictionary<Guid, RollGameState> _events = new();
        private readonly ILogger<RollService> _logger;

        public RollService(ILogger<RollService> logger)
        {
            _logger = logger;
        }

        public Guid CreateEvent(long chatId, long hostUserId, TimeSpan duration)
        {
            var state = new RollGameState(chatId, hostUserId, duration, async (s, timedOut) =>
            {
                try
                {
                    await OnEventEndedAsync(s.EventId, timedOut);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[IRollService]: CreateEvent Timeout handler failed for event {EventId}", s.EventId);
                }
            });

            _events[state.EventId] = state;

            _logger.LogInformation(
                "[IRollService]: Created roll event {EventId} in chat {ChatId} by host {HostUserId} with duration {DurationMinutes}m",
                state.EventId, chatId, hostUserId, duration.TotalMinutes);

            return state.EventId;
        }

        public bool TryGetEvent(Guid eventId, out RollGameState? state)
        {
            var ok = _events.TryGetValue(eventId, out state);
            if (!ok) _logger.LogDebug("[IRollService]: TryGetEvent event {EventId} not found", eventId);
            return ok;
        }

        public (bool Ok, int Value, bool FirstTime) TryRoll(Guid eventId, long userId, string? firstName)
        {
            if (!_events.TryGetValue(eventId, out var s))
            {
                _logger.LogWarning("[IRollService]: TryRoll event {EventId} not found (user {UserId})", eventId, userId);
                return (false, 0, false);
            }

            // race condition protection: game over?
            if (DateTime.UtcNow >= s.EndsAt || s.Cts?.IsCancellationRequested == true)
            {
                _logger.LogInformation("[IRollService]: TryRoll event {EventId} already finished (user {UserId})", eventId, userId);
                return (false, 0, false);
            }

            // Generate the value in advance
            var value = Random.Shared.Next(1, 101);
            var res = new RollResult { UserId = userId, FirstName = firstName, Value = value };

            // Attempt to add atomically
            if (s.Results.TryAdd(userId, res))
            {
                _logger.LogInformation("[IRollService]: TryRoll User {UserId} rolled {Value} in event {EventId}", userId, value, eventId);
                return (true, value, true);
            }
            else
            {
                // return the existing value
                if (s.Results.TryGetValue(userId, out var existing))
                {
                    _logger.LogDebug("[IRollService]: TryRoll User {UserId} attempted second roll in event {EventId}, returning existing {Value}", userId, existing.Value, eventId);
                    return (true, existing.Value, false);
                }

                // very unlikely path, failed to add and no existing one
                _logger.LogWarning("[IRollService]: TryRoll failed to add result for user {UserId} in event {EventId}", userId, eventId);
                return (false, 0, false);
            }
        }

        public void SetMessageId(Guid eventId, int messageId)
        {
            if (_events.TryGetValue(eventId, out var s))
            {
                s.MessageId = messageId;
                _logger.LogDebug("[IRollService]: SetMessageId event {EventId} messageId set to {MessageId}", eventId, messageId);
            }
            else
            {
                _logger.LogWarning("[IRollService]: SetMessageId event {EventId} not found, messageId {MessageId} ignored", eventId, messageId);
            }
        }

        public bool StopEvent(Guid eventId)
        {
            if (_events.TryRemove(eventId, out var s))
            {
                try
                {
                    s.CancelTimeout();
                    s.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[IRollService]: StopEvent error while disposing state for event {EventId}", eventId);
                }

                _logger.LogInformation("[IRollService]: StopEvent event {EventId} stopped by host {HostUserId}", eventId, s.HostUserId);
                return true;
            }

            _logger.LogWarning("[IRollService]: StopEvent event {EventId} not found", eventId);
            return false;
        }

        private Task OnEventEndedAsync(Guid eventId, bool timedOut)
        {
            if (!_events.TryRemove(eventId, out var s))
            {
                _logger.LogDebug("[IRollService]: OnEventEnded event {EventId} already removed", eventId);
                return Task.CompletedTask;
            }

            try
            {
                s.CancelTimeout();
                s.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[IRollService]: OnEventEnded error while disposing state for event {EventId}", eventId);
            }

            _logger.LogInformation("[IRollService]: OnEventEnded event {EventId} ended (timedOut: {TimedOut}) in chat {ChatId}, host {HostUserId}, participants {ParticipantsCount}",
                eventId, timedOut, s.ChatId, s.HostUserId, s.Results.Count);

            return Task.CompletedTask;
        }

    }
}
