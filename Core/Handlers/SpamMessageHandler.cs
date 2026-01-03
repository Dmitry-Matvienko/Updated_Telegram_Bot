using Microsoft.Extensions.Logging;
using MyUpdatedBot.Cache;
using MyUpdatedBot.Services.SpamProtection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class SpamMessageHandler : IMessageHandler
    {
        private readonly ISpamStore _spamStore;
        private readonly IWarning _warning;
        private readonly ILogger<SpamMessageHandler> _logger;
        private const int MaxWarnings = 3;
        private static readonly TimeSpan MuteDuration = TimeSpan.FromHours(24);

        public SpamMessageHandler(ISpamStore spamStore, IWarning warning, ILogger<SpamMessageHandler> logger)
        {
            _spamStore = spamStore;
            _warning = warning;
            _logger = logger;
        }

        public bool CanHandle(Message? message) =>
        message?.From != null && message?.Chat != null;

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            if (message.From is null) return;

            var chatId = message.Chat.Id;
            var userId = message.From.Id;

            // quick in-memory detector
            var isSpam = await _spamStore.AddAndCheckAsync(chatId, userId, ct).ConfigureAwait(false);
            if (!isSpam) return;

            int warnings;
            try
            {
                warnings = await _warning.AddWarningAsync(chatId, userId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SpamMessageHandler]: Failed to AddWarning for {Chat}/{User}", chatId, userId);
                return;
            }

            // Synchronize the warning cache in memory 
            _spamStore.SetCachedWarningsCount(chatId, userId, warnings);

            try
            {
                if (warnings >= MaxWarnings)
                {
                    var member = await botClient.GetChatMember(chatId, userId, ct).ConfigureAwait(false);
                    if (member != null && (member.Status == ChatMemberStatus.Administrator || member.Status == ChatMemberStatus.Creator))
                    {
                        await botClient.SendMessage(chatId, $"⚠️ [{(message.From.FirstName ?? message.From.Username)}](tg://user?id={message.From.Id}), прекрати спамить в чат!", ParseMode.Markdown, replyParameters: message.MessageId, cancellationToken: ct).ConfigureAwait(false);
                        return;
                    }

                    var until = DateTime.UtcNow.Add(MuteDuration);
                    var perms = new ChatPermissions
                    {
                        CanSendMessages = false,
                        CanSendPolls = false,
                        CanSendOtherMessages = false,
                        CanAddWebPagePreviews = false
                    };

                    await botClient.RestrictChatMember(chatId, userId, perms, untilDate: until, cancellationToken: ct).ConfigureAwait(false);
                    await botClient.SendMessage(chatId, $"⚠️ [{(message.From.FirstName ?? message.From.Username)}](tg://user?id={message.From.Id}) получил мут на {MuteDuration} часа (3/3).", ParseMode.Markdown, replyParameters: message.MessageId, cancellationToken: ct).ConfigureAwait(false);
                    _logger.LogInformation("[SpamMessageHandler]: Muted user {User} in chat {Chat}", userId, chatId);
                }
                else
                {
                    await botClient.SendMessage(chatId, $"⚠️ [{(message.From.FirstName ?? message.From.Username)}](tg://user?id={message.From.Id}), перестань спамить. Предупреждение {warnings}/{MaxWarnings}.", ParseMode.Markdown, replyParameters: message.MessageId, cancellationToken: ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SpamMessageHandler]: Failed to act on warning for user {User} in chat {Chat}", userId, chatId);
            }
        }
    }
}
