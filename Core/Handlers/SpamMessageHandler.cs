using Microsoft.Extensions.Logging;
using MyUpdatedBot.Cache.ChatSettingsStore;
using MyUpdatedBot.Cache.SpamStore;
using MyUpdatedBot.Services.ChatSettings;
using MyUpdatedBot.Services.SpamProtection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class SpamMessageHandler : IMessageHandler
    {
        private readonly IFloodStore _spamStore;
        private readonly IWarning _warning;
        private readonly ILogger<SpamMessageHandler> _logger;
        private readonly IChatSettingsService _settingsService;
        private readonly IChatSettingsStore _settingsCache;

        private const int MaxWarnings = 3;
        private static readonly TimeSpan MuteDuration = TimeSpan.FromHours(24);

        public SpamMessageHandler(
            IFloodStore spamStore,
            IWarning warning,
            ILogger<SpamMessageHandler> logger,
            IChatSettingsService settingsService,
            IChatSettingsStore settingsCache)
        {
            _spamStore = spamStore;
            _warning = warning;
            _logger = logger;
            _settingsService = settingsService;
            _settingsCache = settingsCache;
        }

        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
            if (_settingsCache.TryGet(message.Chat.Id, out var cached) && cached != null && cached.SpamProtectionEnabled == false) return false;

            return true;
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var userId = message.From.Id;

            var settings = await _settingsService.GetOrCreateAsync(chatId, ct);
            _settingsCache.Set(chatId, settings);

            if (!settings.SpamProtectionEnabled) return;

            // quick in-memory detector
            var isSpam = await _spamStore.AddAndCheckAsync(chatId, userId, ct);
            if (!isSpam) return;

            int warnings;
            try
            {
                warnings = await _warning.AddWarningAsync(chatId, userId, ct);
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
                    var member = await botClient.GetChatMember(chatId, userId, ct);
                    if (member != null && (member.Status == ChatMemberStatus.Administrator || member.Status == ChatMemberStatus.Creator))
                    {
                        await botClient.SendMessage(chatId, $"⚠️ [{(message.From.FirstName ?? message.From.Username)}](tg://user?id={message.From.Id}), прекрати спамить в чат!", ParseMode.Markdown, replyParameters: message.MessageId, cancellationToken: ct);
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

                    await botClient.RestrictChatMember(chatId, userId, perms, untilDate: until, cancellationToken: ct);
                    await botClient.SendMessage(chatId, $"⚠️ [{(message.From.FirstName ?? message.From.Username)}](tg://user?id={message.From.Id}) получил мут на {MuteDuration.TotalHours} часа (3/3).", ParseMode.Markdown, replyParameters: message.MessageId, cancellationToken: ct);
                    _logger.LogInformation("[SpamMessageHandler]: Muted user {User} in chat {Chat}", userId, chatId);
                }
                else
                {
                    await botClient.SendMessage(chatId, $"⚠️ [{(message.From.FirstName ?? message.From.Username)}](tg://user?id={message.From.Id}), перестань спамить. Предупреждение {warnings}/{MaxWarnings}.", ParseMode.Markdown, replyParameters: message.MessageId, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SpamMessageHandler]: Failed to act on warning for user {User} in chat {Chat}", userId, chatId);
            }
        }
    }
}
