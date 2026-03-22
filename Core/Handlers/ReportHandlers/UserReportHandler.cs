using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Handlers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using MyUpdatedBot.Cache.ReportsStore;
using MyUpdatedBot.Core.Localization;

public class UserReportHandler : IMessageHandler
{
    private readonly ILogger<UserReportHandler> _logger;
    private readonly IUserReportsThrottleStore _throttle;
    private readonly LocalizationUtil _loc;

    public UserReportHandler(ILogger<UserReportHandler> logger, IUserReportsThrottleStore throttle, LocalizationUtil loc)
    {
        _logger = logger;
        _throttle = throttle;
        _loc = loc;
    }

    public bool CanHandle(Message? message)
    {
        if (message?.From == null || message.Chat == null || message.From.IsBot) return false;
        if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup) return false;
        if (string.IsNullOrWhiteSpace(message.Text)) return false;

        return message.Text.Equals("!админ", StringComparison.OrdinalIgnoreCase)
            || message.Text.Equals("!адмін", StringComparison.OrdinalIgnoreCase)
            || message.Text.Equals("!report", StringComparison.OrdinalIgnoreCase);
    }

    public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        if (message.ReplyToMessage is null)
        {
            await botClient.SendMessage(chatId, await _loc.GetStringAsync(chatId, message, "NeedMessageForReport"),
                cancellationToken: ct);
            return;
        }

        var reporterId = message.From!.Id;
        var target = message.ReplyToMessage.From;
        if (target == null) return;

        if (reporterId == target.Id)
        {
            await botClient.SendMessage(chatId, await _loc.GetStringAsync(chatId, message, "CantComplYourself"),
                replyParameters: message.MessageId,
                cancellationToken: ct);
            return;
        }

        // throttle
        var key = (chat: chatId, user: reporterId);
        if (!_throttle.TryCheckAndSet(key, out var waitSeconds))
        {
            await botClient.SendMessage(chatId,
                $"{waitSeconds} {await _loc.GetStringAsync(chatId, message, "TimeLeftForNextCompl")}",
                replyParameters: message.MessageId,
                cancellationToken: ct);
            return;
        }

        ChatMember[] admins;
        try
        {
            var adminsList = await botClient.GetChatAdministrators(chatId, ct);
            admins = adminsList.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReportHandler]: Unable to obtain chat administrators {ChatId}", chatId);

            await botClient.SendMessage(chatId, await _loc.GetStringAsync(chatId, message, "CantGetAdminsList"),
                replyParameters: message.MessageId,
                cancellationToken: ct);
            return;
        }

        var buttons = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData(await _loc.GetStringAsync(chatId, message, "MuteButton"), $"compl:{chatId}:{message.ReplyToMessage.MessageId}:{target.Id}:mute30"),
                InlineKeyboardButton.WithCallbackData(await _loc.GetStringAsync(chatId, message, "BanButton"), $"compl:{chatId}:{message.ReplyToMessage.MessageId}:{target.Id}:ban")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData(await _loc.GetStringAsync(chatId, message, "IgnoreButton"), $"compl:{chatId}:{message.ReplyToMessage.MessageId}:{target.Id}:ignore")
            }
        });

        var notifyText = $"{await _loc.GetStringAsync(chatId, message, "ComplaintIn")} {(message.Chat.Title ?? message.Chat.Id.ToString())}\n" +
                         $"{await _loc.GetStringAsync(chatId, message, "ComplaintFrom")} [{(message.From.FirstName ?? message.From.Username ?? message.From.Id.ToString())}](tg://user?id={reporterId})\n" +
                         $"{await _loc.GetStringAsync(chatId, message, "ComplaintAgainst")} [{(target.FirstName ?? target.Username ?? target.Id.ToString())}](tg://user?id={target.Id})\n";

        foreach (var admin in admins)
        {
            if (admin.User.IsBot) continue;

            var adminId = admin.User.Id;

            try
            {
                await botClient.SendMessage(
                    chatId: adminId,
                    text: notifyText,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: buttons,
                    cancellationToken: ct);

                await botClient.ForwardMessage(
                    chatId: adminId,
                    fromChatId: chatId,
                    messageId: message.ReplyToMessage.MessageId,
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ReportHandler]: Failed to notify admin {AdminId}. User didn't start the bot", adminId);
            }
        }

        try { await botClient.DeleteMessage(chatId, messageId: message.MessageId, cancellationToken: ct); }catch {}
        await botClient.SendMessage(chatId, await _loc.GetStringAsync(chatId, message, "ComplaintSent"), cancellationToken: ct);
    }
}