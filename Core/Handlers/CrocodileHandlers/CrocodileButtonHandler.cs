using Microsoft.Extensions.Logging;
using Mono.TextTemplating;
using MyUpdatedBot.Services.CrocodileGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers.CrocodileHandlers
{
    public class CrocodileButtonHandler : IButtonHandlers
    {
        private readonly ICrocodileService _games;
        private readonly ILogger<CrocodileButtonHandler> _logger;

        public CrocodileButtonHandler(ICrocodileService games, ILogger<CrocodileButtonHandler> logger)
        {
            _games = games;
            _logger = logger;
        }
        public bool CanHandle(CallbackQuery callback)
        {
            // process buttons if there is an active game in this chat
            return callback.Message is { Chat.Id: var chatId }
                && _games.HasGame(chatId);
        }

        public async Task HandleAsync(ITelegramBotClient client, CallbackQuery callback, CancellationToken ct)
        {
            var chatId = callback.Message!.Chat.Id;
            // return if there isnt the game
            if (!_games.TryGetGameState(chatId, out var state))
                return;

            // check that host press the button
            var isHost = callback.From!.Id == state.HostUserId;
            string response;

            switch (callback.Data)
            {
                
                case "show_word" when isHost:
                    response = $"🔎 Слово: {state.CurrentWord}";
                    await client.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                case "show_word":
                    response = "⚠️ Только ведущий может видеть слово!";
                    await client.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                case "change_word" when isHost:
                    if (_games.TryChangeWord(chatId, out var newWord))
                        response = $"🔄 Новое слово: {newWord}";
                    else
                        response = "❌ Не удалось сменить слово.";
                    await client.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                case "change_word":
                    response = "⚠️ Только ведущий может сменить слово!";
                    await client.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                case "end_game" when isHost:
                    _games.EndGame(chatId);
                    await client.SendMessage(
                        chatId: chatId,
                        text: $"🛑 [{callback.From.FirstName}](tg://user?id={callback.From.Id}) завершил(а) игру. Чтобы начать новую, напиши /crocodile.",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct);
                    return;

                case "end_game":
                    response = "⚠️ Только ведущий может завершить игру!";
                    await client.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                default:
                    await client.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
                    return;
            }
        }

    }
}
