using MyUpdatedBot.Core.Localization;
using MyUpdatedBot.Services.CrocodileGame;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers.CrocodileHandlers
{
    public class CrocodileCallbackHandler : IButtonHandlers
    {
        private readonly ICrocodileService _games;
        private readonly LocalizationUtil _loc;

        public CrocodileCallbackHandler(ICrocodileService games, LocalizationUtil loc)
        {
            _games = games;
            _loc = loc;
        }
        public bool CanHandle(CallbackQuery callback)
        {
            // process buttons if there is an active game in this chat
            return callback.Message is { Chat.Id: var chatId }
                && _games.HasGame(chatId);
        }

        public async Task HandleAsync(ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct)
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
                    response = $"🔎 {await _loc.GetStringAsync(chatId, callback.Message, "CallbackQuery_Word")}: {state.CurrentWord}";
                    await botClient.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                case "show_word":
                    response = $"⚠️ {await _loc.GetStringAsync(chatId, callback.Message, "OnlyHostSeeWord")}";
                    await botClient.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                case "change_word" when isHost:
                    if (_games.TryChangeWord(chatId, out var newWord))
                        response = $"🔄 {await _loc.GetStringAsync(chatId, callback.Message, "CallbackQuery_NewWord")}: {newWord}";
                    else
                        response = $"❌ {await _loc.GetStringAsync(chatId, callback.Message, "CallbackQuery_CantChangeWord")}";
                    await botClient.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                case "change_word":
                    response = $"⚠️ {await _loc.GetStringAsync(chatId, callback.Message, "OnlyHostChangeWord")}";
                    await botClient.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                case "end_game" when isHost:
                    _games.EndGame(chatId);
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"🛑 [{callback.From.FirstName}](tg://user?id={callback.From.Id}) {await _loc.GetStringAsync(chatId, callback.Message, "Crocodile_EndedGame")} /crocodile.",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct);
                    return;

                case "end_game":
                    response = $"⚠️ {await _loc.GetStringAsync(chatId, callback.Message, "OnlyHostEndGame")}";
                    await botClient.AnswerCallbackQuery(
                        callback.Id,
                        response,
                        showAlert: true,
                        cancellationToken: ct);
                    return;

                default:
                    await botClient.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
                    return;
            }
        }

    }
}
