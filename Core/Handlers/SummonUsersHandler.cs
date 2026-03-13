using MyUpdatedBot.Services.SummonUsers;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class SummonUsersHandler : IMessageHandler
    {
        private readonly ISummonUsers _stats;

        private const int DefaultLimit = 80;         // take sorted users from DB
        private const int BatchSize = 20;            // Mention users per message
        private const int DelayMsBetweenBatches = 250;
        private static readonly Random _rnd = new Random();


        private static readonly string[] Emojis = new[]
        {
            "📣","🔔","🎯","🔥","✨","🚀","📢","🎉","🛎️","🔊",
            "📯","🎺","📡","🪧","💥","⚡","🎈","🚨","🌟","🎷",
            "🎸","🥁","🪘","🪇","🎻","📯","🪄","🏳️‍🌈","💫","⭐"
        };

        public SummonUsersHandler(ISummonUsers stats)
        {
            _stats = stats;
        }

        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
            if (string.IsNullOrWhiteSpace(message.Text)) return false;

            var t = message.Text.Trim();
            return t.StartsWith("/all", StringComparison.OrdinalIgnoreCase);
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var userId = message.From!.Id;

            try
            {
                var member = await botClient.GetChatMember(chatId, userId, ct);
                if (member is null || (member.Status != ChatMemberStatus.Administrator && member.Status != ChatMemberStatus.Creator))
                {
                    await botClient.SendMessage(chatId, "Команду могут вызывать только администраторы.", replyParameters: message.MessageId, cancellationToken: ct);
                    return;
                }
            }
            catch (Exception)
            {
                await botClient.SendMessage(chatId, "Не удалось проверить ваши права. Повторите позднее.", replyParameters: message.MessageId, cancellationToken: ct);
                return;
            }

            var users = await _stats.GetUsersAsync(chatId, DefaultLimit, ct);
            if (users == null || users.Count == 0)
            {
                await botClient.SendMessage(chatId, "Не нашёл пользователей для созыва.", cancellationToken: ct);
                return;
            }

            var mentions = users.Select(u =>
            {
                var emoji = EmojiForUserRandom();
                return $"<a href=\"tg://user?id={u.TelegramId}\">{emoji}</a>";
            }).ToList();

            await botClient.SendMessage(chatId, "📢 Начат созыв всех активных пользователей!", cancellationToken: ct);

            var sb = new StringBuilder();
            for (int offset = 0; offset < mentions.Count; offset += BatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var chunk = mentions.Skip(offset).Take(BatchSize).ToList();

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(chunk[i]);
                }

                var textToSend = sb.ToString();
                if (string.IsNullOrWhiteSpace(textToSend)) continue;

                try
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: textToSend,
                        parseMode: ParseMode.Html,
                        replyParameters: message.MessageId,
                        cancellationToken: ct);
                }
                catch (Exception)
                {
                }

                await Task.Delay(DelayMsBetweenBatches, ct);
            }

            await botClient.SendMessage(chatId, "📢 Созыв окончен!", cancellationToken: ct);

        }

        private static string EmojiForUserRandom()
        {
            return Emojis[_rnd.Next(Emojis.Length)];
        }
    }
}
