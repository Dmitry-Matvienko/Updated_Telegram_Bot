using Microsoft.Extensions.Logging;
using MyUpdatedBot.Cache.ChatSettingsStore;
using MyUpdatedBot.Services.ChatSettings;
using MyUpdatedBot.Services.SpamProtection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MyUpdatedBot.Core.Handlers
{
    public class LinkDetectionHandler : IMessageHandler
    {
        private readonly IWarning _warning;
        private readonly ILogger<LinkDetectionHandler> _logger;
        private readonly IChatSettingsService _settingsService;
        private readonly IChatSettingsStore _settingsCache;

        private const int MaxWarnings = 3;
        private static readonly TimeSpan MuteDuration = TimeSpan.FromHours(24);

        public LinkDetectionHandler(IWarning warning, ILogger<LinkDetectionHandler> logger,
                                    IChatSettingsService settingsService, IChatSettingsStore settingsCache)
        {
            _warning = warning;
            _logger = logger;
            _settingsService = settingsService;
            _settingsCache = settingsCache;
        }

        public bool CanHandle(Message? message)
        {
            if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
            if (string.IsNullOrWhiteSpace(message.Text ?? message.Caption)) return false;

            // if cached and LinksAllowed
            if (_settingsCache.TryGet(message.Chat.Id, out var cached) && cached != null && cached.LinksAllowed == true) return false;

            var entities = message.Entities ?? message.CaptionEntities;
            if (entities != null && entities.Any(e =>
                e.Type == MessageEntityType.Url ||
                e.Type == MessageEntityType.TextLink ||
                e.Type == MessageEntityType.Email))
            {
                return true;
            }

            return false;
        }

        public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
        {
            var settings = await _settingsService.GetOrCreateAsync(message.Chat.Id, ct);
            _settingsCache.Set(message.Chat.Id, settings);

            if (settings.LinksAllowed) return;


            if (TryGetLinkFromEntities(message, out var matched))
            {
                await ProcessFoundLink(botClient, message, matched, ct);
                return;
            }
        }

        private bool TryGetLinkFromEntities(Message msg, out string? matched)
        {
            matched = null;
            var entities = msg.Entities ?? msg.CaptionEntities;
            if (entities == null) return false;

            var text = msg.Text ?? msg.Caption ?? string.Empty;
            foreach (var e in entities)
            {
                if (e.Type == MessageEntityType.TextLink && !string.IsNullOrEmpty(e.Url))
                {
                    matched = e.Url;
                    return true;
                }

                if (e.Type == MessageEntityType.Url || e.Type == MessageEntityType.Email)
                {
                    if (e.Offset >= 0 && e.Length >= 0 && e.Offset + e.Length <= text.Length)
                    {
                        matched = text.Substring(e.Offset, e.Length);
                        return true;
                    }
                }
            }
            return false;
        }

        private async Task ProcessFoundLink(ITelegramBotClient botClient, Message message, string? matched, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var userId = message.From!.Id;

            _logger.LogInformation("[LinkDetectionHandler]: detected link in chat {Chat} from user {User}. Matched: {Match}",
                chatId, userId, matched);

            ChatMember? member = null;
            try
            {
                member = await botClient.GetChatMember(chatId, userId, ct);
                if (member != null && (member.Status == ChatMemberStatus.Administrator || member.Status == ChatMemberStatus.Creator)) return;
                await botClient.DeleteMessage(chatId, message.MessageId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LinkDetectionHandler]: failed to delete message {MessageId} in chat {Chat}", message.MessageId, chatId);
            }

            int warnings;
            try
            {
                warnings = await _warning.AddWarningAsync(chatId, userId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LinkDetectionHandler]: failed to AddWarning for chat {Chat} user {User}", chatId, userId);
                return;
            }

            try
            {
                if (warnings >= MaxWarnings)
                {
                    var perms = new ChatPermissions
                    {
                        CanSendMessages = false,
                        CanSendPolls = false,
                        CanSendOtherMessages = false,
                        CanAddWebPagePreviews = false
                    };

                    var until = DateTime.UtcNow.Add(MuteDuration);
                    await botClient.RestrictChatMember(chatId, userId, perms, untilDate: until, cancellationToken: ct);
                    await botClient.SendMessage(chatId, $"⚠️ [{(message.From.FirstName ?? message.From.Username)}](tg://user?id={message.From.Id}) получил мут на {MuteDuration} часa (3/3).", parseMode: ParseMode.Markdown, cancellationToken: ct);
                    _logger.LogInformation("[LinkDetectionHandler]: muted user {User} in chat {Chat}", userId, chatId);
                }
                else
                {
                    await botClient.SendMessage(chatId, $"⚠️ [{(message.From.FirstName ?? message.From.Username)}](tg://user?id={message.From.Id}), в этом чате нельзя отправлять ссылки. Предупреждение {warnings}/{MaxWarnings}.", parseMode: ParseMode.Markdown,  cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LinkDetectionHandler]: failed to notify or mute user {User} in chat {Chat}", userId, chatId);
            }
        }

    }
}
