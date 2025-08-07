using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Services.CrocodileGame
{
    public class CrocodileService : ICrocodileService
    {
        private readonly ConcurrentDictionary<long, GameState> _games = new();
        private readonly WordRepository _repo;
        private readonly ITelegramBotClient _bot;
        private readonly ILogger<CrocodileService> _logger;

        public CrocodileService(WordRepository repo, ITelegramBotClient bot, ILogger<CrocodileService> logger)
        {
            _repo = repo;
            _bot = bot;
            _logger = logger;
        }

        public bool TryStartGame(long chatId, long userId, out string word)
        {
            _logger.LogDebug("[CrocodileService]: TryStartGame started");
            if (_games.ContainsKey(chatId))
            {
                _logger.LogWarning("[CrocodileService]: attempt to start a new game in chat {ChatId}, but the game is already in progress", chatId);
                word = null!;
                return false;
            }

            word = _repo.GetRandom();

            _logger.LogInformation("[CrocodileService]: start the game in chat {ChatId} with host {UserId}. Guessed word: {Word}", chatId, userId, word);

            var state = new GameState(chatId, userId, word, onTimeout: async cid =>
            {
                //  inform users that the game has ended by timeout
                await _bot.SendMessage(
                  chatId: cid,
                  text: "⏰ Время игры истекло! Чтобы начать новую — отправьте /crocodile",
                  parseMode: ParseMode.Markdown);
                EndGame(cid);
            });
            _games[chatId] = state;
            return true;
        }
        public bool HasGame(long chatId) => _games.ContainsKey(chatId);

        public bool TryChangeWord(long chatId, out string word)
        {
            
            if (!_games.TryGetValue(chatId, out var state))
            {
                word = null!;
                return false;
            }

            word = _repo.GetRandom();
            state.CurrentWord = word;
            _logger.LogInformation("[CrocodileService]: Word changed in chat {ChatId}. New word: {Word}", chatId, word);
            return true;
        }

        public bool TryGuess(long chatId, long userId, string guess, out bool correct)
        {
            if (_games.TryGetValue(chatId, out var state) &&
                !string.IsNullOrEmpty(guess))
            {
                correct = string.Equals(guess.Trim(), state.CurrentWord, StringComparison.OrdinalIgnoreCase);
                _logger.LogInformation("[CrocodileService]: User {UserId} in chat {ChatId} tried to guess the word. Correct: {Correct}", userId, chatId, correct);
                return true;
            }
            correct = false;
            return false;
        }

        public bool TryGetGameState(long chatId, out GameState state)
        {
            _logger.LogDebug("[CrocodileService]: TryGetGameState receiving state");
            return _games.TryGetValue(chatId, out state!);
        }

        public void EndGame(long chatId)
        {
            if (_games.TryRemove(chatId, out var state))
            {
                //_botClient.SendMessage(chatId, "Игра окончена");
            }
            _logger.LogInformation("[CrocodileService]: EndGame the game ended in chat {ChatId}", chatId);
        }
    }
}
