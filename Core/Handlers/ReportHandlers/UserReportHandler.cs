using Microsoft.Extensions.Logging;
using MyUpdatedBot.Core.Handlers;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class ReportHandler : ICommandHandler
{
    private readonly ILogger<ReportHandler> _logger;

    // in-memory throttle
    private static readonly ConcurrentDictionary<(long chat, long user), DateTime> _throttle = new();

    // delay between complaints from one user in one chat
    private readonly TimeSpan _throttleDelay = TimeSpan.FromSeconds(180);

    public ReportHandler(ILogger<ReportHandler> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        return t.Equals("!админ", StringComparison.OrdinalIgnoreCase)
            || t.Equals("!report", StringComparison.OrdinalIgnoreCase);
    }

    public async Task HandleAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        
        if (message.ReplyToMessage is null)
        {
            await botClient.SendMessage(message.Chat.Id,
                "Нужно писать эту команду на сообщение, которое нарушет правила чата",
                cancellationToken: ct);
            return;
        }

        var chatId = message.Chat.Id;
        var reporterId = message.From!.Id;
        var target = message.ReplyToMessage.From;
        if (target == null) return;

        if (reporterId == target.Id)
        {
            await botClient.SendMessage(chatId,
                "Нельзя пожаловаться на самого себя",
                replyParameters: message.MessageId,
                cancellationToken: ct);
            return;
        }

        // throttle
        var key = (chat: chatId, user: reporterId);
        var now = DateTime.UtcNow;
        if (_throttle.TryGetValue(key, out var allowedAfter) && allowedAfter > now)
        {
            var wait = (int) (allowedAfter - now).TotalSeconds;
            await botClient.SendMessage(chatId,
                $"Подожди {wait} сек. прежде чем отправлять следующую жалобу",
                replyParameters: message.MessageId,
                cancellationToken: ct);
            return;
        }
        _throttle[key] = now.Add(_throttleDelay);

        ChatMember[] admins;
        try
        {
            var adminsList = await botClient.GetChatAdministrators(chatId, ct);
            admins = adminsList.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReportHandler]: Unable to obtain chat administrators {ChatId}", chatId);
            await botClient.SendMessage(chatId,
                "Не могу получить список администраторов. Попробуйте позже.",
                replyParameters: message.MessageId,
                cancellationToken: ct);
            return;
        }

        InlineKeyboardMarkup BuildButtons() =>
            new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("🔇 Мут 30 мин", $"compl:{chatId}:{message.ReplyToMessage.MessageId}:{target.Id}:mute30"),
                    InlineKeyboardButton.WithCallbackData("⛔ Бан", $"compl:{chatId}:{message.ReplyToMessage.MessageId}:{target.Id}:ban")
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("➖ Игнор", $"compl:{chatId}:{message.ReplyToMessage.MessageId}:{target.Id}:ignore")
                }
            });

        var buttons = BuildButtons();

        var notifyText = $"⚠️ Жалоба в чате {(message.Chat.Title ?? message.Chat.Id.ToString())}\n" +
                         $"Жалоба от: [{(message.From.FirstName ?? message.From.Username ?? message.From.Id.ToString())}](tg://user?id={reporterId})\n" +
                         $"На: [{(target.FirstName ?? target.Username ?? target.Id.ToString())}](tg://user?id={target.Id})\n" +
                         $"(переслано ниже)";

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

        await botClient.DeleteMessage(chatId, messageId: message.MessageId, cancellationToken: ct);
        await botClient.SendMessage(chatId,"Жалоба отправлена администраторам. Спасибо.",cancellationToken: ct);
    }
}