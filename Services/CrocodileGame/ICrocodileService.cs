using MyUpdatedBot.Core.Models;

namespace MyUpdatedBot.Services.CrocodileGame
{
    public interface ICrocodileService
    {
        bool TryStartGame(long chatId, long userId, out string word);
        bool TryChangeWord(long chatId, out string word);
        bool TryGuess(long chatId, long userId, string guess, out bool correct);
        void EndGame(long chatId);
        bool TryGetGameState(long chatId, out GameState state);
        bool HasGame(long chatId);
    }
}
