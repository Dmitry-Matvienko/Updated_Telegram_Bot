using Microsoft.Extensions.Hosting;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace MyUpdatedBot.Services.CrocodileGame
{
    public class WordRepository
    {
        private readonly ImmutableArray<string> _words;
        private readonly Random _rng = new();

        public WordRepository(IHostEnvironment env)
        {
            var path = Path.Combine(env.ContentRootPath, "crocodile-words.txt");
            _words = File.ReadAllLines(path)
                         .Where(l => !string.IsNullOrWhiteSpace(l))
                         .Select(l => l.Trim())
                         .ToImmutableArray();
        }

        public string GetRandom() => _words[_rng.Next(_words.Length)];
    }
}
