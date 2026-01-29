using MyUpdatedBot.Core.Models;
using MyUpdatedBot.Services.RollGame;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers.RollGameHandlers
{
    public class RollGameButtonHandler : IButtonHandlers
    {
        private readonly IRollService _rollService;

        public RollGameButtonHandler(IRollService rollService) { _rollService = rollService; }

        public bool CanHandle(CallbackQuery callback) => !string.IsNullOrEmpty(callback.Data)
        && (callback.Data.StartsWith("roll:") || callback.Data.StartsWith("stop:"));

        public async Task HandleAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct)
        {
            var data = callback.Data!;
            if (data.StartsWith("roll:"))
            {
                if (!Guid.TryParseExact(data.Substring(5), "N", out var id))
                {
                    await botClient.AnswerCallbackQuery(callback.Id, "Некорректный идентификатор", cancellationToken: ct);
                    return;
                }

                var rollResult = _rollService.TryRoll(id, callback.From.Id, callback.From.FirstName);

                if (!rollResult.Ok)
                {
                    await botClient.AnswerCallbackQuery(callback.Id, "Игра уже закончена", showAlert: true, cancellationToken: ct);
                    return;
                }

                await botClient.AnswerCallbackQuery(callback.Id, rollResult.FirstTime ? $"Твой бросок: {rollResult.Value}" : $"Ты уже бросал: {rollResult.Value}", showAlert: true, cancellationToken: ct);

                if (_rollService.TryGetEvent(id, out var state) && state.MessageId != 0)
                {
                    await state.EditLock.WaitAsync(ct);
                    try
                    {
                        var text = BuildLeaderBoardText(state, finished: false);
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
                    await botClient.AnswerCallbackQuery(callback.Id, "Некорректный идентификатор", cancellationToken: ct);
                    return;
                }

                if (!_rollService.TryGetEvent(id, out var state))
                {
                    await botClient.AnswerCallbackQuery(callback.Id, "Игра уже закончена", showAlert: true, cancellationToken: ct);
                    return;
                }

                if (callback.From.Id != state.HostUserId)
                {
                    await botClient.AnswerCallbackQuery(callback.Id, "Только ведущий может остановить розыгрыш", showAlert: true, cancellationToken: ct);
                    return;
                }

                _rollService.StopEvent(id);
                await botClient.AnswerCallbackQuery(callback.Id, "Розыгрыш остановлен", showAlert: true, cancellationToken: ct);

                var finalText = BuildLeaderBoardText(state, finished: true);
                await botClient.EditMessageText(state.ChatId, state.MessageId, "🛑 Розыгрыш остановлен\n\n" + finalText, ParseMode.Markdown, replyMarkup: null, cancellationToken: ct);
            }
        }

        private string BuildLeaderBoardText(RollGameState state, bool finished)
        {
            var sb = new StringBuilder();

            if (!state.Results.Any())
            {
                sb.AppendLine("_Пока нет участников_");
            }
            else
            {
                var ordered = state.Results.Values
                    .OrderByDescending(r => r.Value)
                    .ThenBy(r => r.FirstName)
                    .ToList();

                sb.AppendLine("Победители:");

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
                    sb.AppendLine("\nОстальные участники:");
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
            sb.AppendLine(finished ? "Розыгрыш окончен!" : $"До конца: {timeStr}");

            return sb.ToString();
        }
    }

}
