using MyUpdatedBot.Core.Localization;
using MyUpdatedBot.Core.Models;
using MyUpdatedBot.Services.RollGame;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers.RollGameHandlers
{
    public class RollGameCallbackHandler : IButtonHandlers
    {
        private readonly IRollService _rollService;
        private readonly LocalizationUtil _loc;

        public RollGameCallbackHandler(IRollService rollService, LocalizationUtil loc)
        { 
            _rollService = rollService; 
            _loc = loc;
        }

        public bool CanHandle(CallbackQuery callback) => !string.IsNullOrEmpty(callback.Data)
        && (callback.Data.StartsWith("roll:") || callback.Data.StartsWith("stop:"));

        public async Task HandleAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct)
        {
            var data = callback.Data!;
            var chatId = callback.Message!.Chat.Id;
            if (data.StartsWith("roll:"))
            {
                if (!Guid.TryParseExact(data.Substring(5), "N", out var id))
                {
                    await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(chatId, callback.Message, "UnknownCommand"), cancellationToken: ct);
                    return;
                }

                var rollResult = _rollService.TryRoll(id, callback.From.Id, callback.From.FirstName);

                if (!rollResult.Ok)
                {
                    await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(chatId, callback.Message, "GameAlreadyEnded"), showAlert: true, cancellationToken: ct);
                    return;
                }

                await botClient.AnswerCallbackQuery(callback.Id, rollResult.FirstTime ? 
                    $"{await _loc.GetStringAsync(chatId, callback.Message, "UserThrow")}: {rollResult.Value}" : 
                    $"{await _loc.GetStringAsync(chatId, callback.Message, "AlreadyRolled")}: {rollResult.Value}", showAlert: true, cancellationToken: ct);

                if (_rollService.TryGetEvent(id, out var state) && state.MessageId != 0)
                {
                    await state.EditLock.WaitAsync(ct);
                    try
                    {
                        var text = await BuildLeaderBoardTextAsync(state, finished: false, callback.Message);
                        await botClient.EditMessageText(state.ChatId, state.MessageId, text, ParseMode.Markdown, replyMarkup: callback.Message!.ReplyMarkup, cancellationToken: ct);
                    }
                    finally
                    {
                        state.EditLock.Release();
                    }
                }
            }
            else if (data.StartsWith("stop:"))
            {
                if (!Guid.TryParseExact(data.Substring(5), "N", out var id))
                {
                    await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(chatId, callback.Message, "UnknownCommand"), cancellationToken: ct);
                    return;
                }

                if (!_rollService.TryGetEvent(id, out var state))
                {
                    await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(chatId, callback.Message, "GameAlreadyEnded"), showAlert: true, cancellationToken: ct);
                    return;
                }

                if (callback.From.Id != state.HostUserId)
                {
                    await botClient.AnswerCallbackQuery(callback.Id, await _loc.GetStringAsync(chatId, callback.Message, "OnlyHostEndGame"), showAlert: true, cancellationToken: ct);
                    return;
                }

                _rollService.StopEvent(id);
                var EventStopped = await _loc.GetStringAsync(chatId, callback.Message, "EventStopped");
                await botClient.AnswerCallbackQuery(callback.Id, EventStopped, showAlert: true, cancellationToken: ct);

                var finalText = BuildLeaderBoardTextAsync(state, finished: true);
                await botClient.EditMessageText(state.ChatId, state.MessageId, $"🛑 {EventStopped}\n\n" + finalText, ParseMode.Markdown, replyMarkup: null, cancellationToken: ct);
            }
        }

        private async Task<string> BuildLeaderBoardTextAsync(RollGameState state, bool finished, Message? message = null)
        {
            var sb = new StringBuilder();

            if (!state.Results.Any())
            {
                sb.AppendLine(await _loc.GetStringAsync(state.ChatId, message, "NoParticipantsYet"));
            }
            else
            {
                var ordered = state.Results.Values
                    .OrderByDescending(r => r.Value)
                    .ThenBy(r => r.FirstName)
                    .ToList();

                sb.AppendLine(await _loc.GetStringAsync(state.ChatId, message, "WinnersEvent"));

                var medals = new[] { "🥇", "🥈", "🥉" };

                int rank = 1;

                for (int i = 0; i < Math.Min(3, ordered.Count); i++, rank++)
                {
                    var r = ordered[i];
                    var medal = medals[i];
                    sb.AppendLine($"{medal} [{r.FirstName}](tg://user?id={r.UserId}) — *{r.Value}*");
                }

                var rest = ordered.Skip(3).Take(7).ToList();
                if (rest.Any())
                {
                    sb.AppendLine($"\n{await _loc.GetStringAsync(state.ChatId, message, "OtherParticipants")}:");
                    foreach (var r in rest)
                    {
                        sb.AppendLine($"{rank}. [{r.FirstName}](tg://user?id={r.UserId}) — *{r.Value}*");
                        rank++;
                    }
                }
            }

            sb.AppendLine();

            var remaining = (int) (state.EndsAt - DateTime.UtcNow).TotalSeconds;
            if (remaining < 0) remaining = 0;
            var timeStr = TimeSpan.FromSeconds(remaining).ToString(@"mm\:ss");
            sb.AppendLine(finished ?
                await _loc.GetStringAsync(state.ChatId, message, "GameAlreadyEnded") :
                $"{await _loc.GetStringAsync(state.ChatId, message, "UntilTheEnd")}: {timeStr}");

            return sb.ToString();
        }
    }

}
