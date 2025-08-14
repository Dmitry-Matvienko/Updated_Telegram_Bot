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

        public async Task HandleAsync(ITelegramBotClient client, CallbackQuery callback, CancellationToken ct)
        {
            var data = callback.Data!;
            if (data.StartsWith("roll:"))
            {
                if (!Guid.TryParseExact(data.Substring(5), "N", out var id))
                {
                    await client.AnswerCallbackQuery(callback.Id, "Некорректный идентификатор", cancellationToken: ct);
                    return;
                }

                var res = _rollService.TryRoll(id, callback.From.Id, callback.From.FirstName);

                if (!res.Ok)
                {
                    await client.AnswerCallbackQuery(callback.Id, "Игра уже закончена", showAlert: true, cancellationToken: ct);
                    return;
                }

                await client.AnswerCallbackQuery(callback.Id, res.FirstTime ? $"Твой бросок: {res.Value}" : $"Ты уже бросал: {res.Value}", showAlert: true, cancellationToken: ct);

                if (_rollService.TryGetEvent(id, out var state) && state.MessageId != 0)
                {
                    await state.EditLock.WaitAsync(ct);
                    try
                    {
                        var text = BuildLeaderBoardText(state, finished: false);
                        await client.EditMessageText(state.ChatId, state.MessageId, text, ParseMode.Markdown, replyMarkup: callback.Message!.ReplyMarkup, cancellationToken: ct);
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
                    await client.AnswerCallbackQuery(callback.Id, "Некорректный идентификатор", cancellationToken: ct);
                    return;
                }

                if (!_rollService.TryGetEvent(id, out var state))
                {
                    await client.AnswerCallbackQuery(callback.Id, "Игра уже закончена", showAlert: true, cancellationToken: ct);
                    return;
                }

                if (callback.From.Id != state.HostUserId)
                {
                    await client.AnswerCallbackQuery(callback.Id, "Только ведущий может остановить розыгрыш", showAlert: true, cancellationToken: ct);
                    return;
                }

                _rollService.StopEvent(id);
                await client.AnswerCallbackQuery(callback.Id, "Розыгрыш остановлен", showAlert: true, cancellationToken: ct);

                var finalText = BuildLeaderBoardText(state, finished: true);
                await client.EditMessageText(state.ChatId, state.MessageId, "🛑 Розыгрыш остановлен\n\n" + finalText, ParseMode.Markdown, replyMarkup: null, cancellationToken: ct);
            }
        }

        private string BuildLeaderBoardText(RollGameState state, bool finished)
        {
            var sb = new StringBuilder();
            if (!state.Results.Any()) sb.AppendLine("_Пока нет участников_");
            else
            {
                var top = state.Results.Values.OrderByDescending(r => r.Value).Take(10).Select((r, i) => $"{i + 1}. [{r.FirstName}](tg://user?id={r.UserId}) — *{r.Value}*");
                sb.AppendLine("Победители:");
                foreach (var s in top) sb.AppendLine(s);
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
