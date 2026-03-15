using Microsoft.Extensions.Logging;
using MyUpdatedBot.Cache.ChatSettingsStore;
using MyUpdatedBot.Core.Models.Entities;
using MyUpdatedBot.Services.ChatSettings;
using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using Telegram.Bot.Types;

namespace MyUpdatedBot.Core.Localization
{
    public class LocalizationUtil
    {
        private readonly IChatSettingsStore _settingsCache;
        private readonly IChatSettingsService? _settingsService;
        private readonly ResourceManager _rm;
        private readonly ILogger<LocalizationUtil>? _logger;
        private readonly ConcurrentDictionary<string, string> _cache = new();

        public LocalizationUtil(
            IChatSettingsStore settingsCache,
            IChatSettingsService? settingsService = null,
            ResourceManager? resourceManager = null,
            ILogger<LocalizationUtil>? logger = null)
        {
            _settingsCache = settingsCache ?? throw new ArgumentNullException(nameof(settingsCache));
            _settingsService = settingsService;
            _rm = resourceManager ?? Resources.Messages.ResourceManager;
            _logger = logger;
        }

        /// <summary>
        /// Use where ChatSettingsEntity is present. Does not make calls to the IChatSettingsStore or the database
        /// </summary>
        public string GetString(ChatSettingsEntity settings, string resourceKey)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return GetStringForCulture(resourceKey, settings.Language);
        }

        /// <summary>
        /// Use where ChatSettingsEntity is not present. Check cache and if it's not there, makes a database query
        /// </summary>
        public async Task<string> GetStringAsync(long chatId, Message? message, string resourceKey, CancellationToken ct = default)
        {
            if (_settingsCache.TryGet(chatId, out var settings) && !string.IsNullOrWhiteSpace(settings?.Language))
            {
                return GetStringForCulture(resourceKey, settings.Language);
            }

            if (_settingsService != null)
            {
                try
                {
                    var s = await _settingsService.GetOrCreateAsync(chatId, ct).ConfigureAwait(false);
                    if (s != null)
                    {
                        try { _settingsCache.Set(chatId, s); } catch { }

                        if (!string.IsNullOrWhiteSpace(s.Language))
                        {
                            var culture = s.Language;
                            return GetStringForCulture(resourceKey, culture);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "[LocalizationUtil]: GetOrCreateAsync failed for chat {ChatId}", chatId);
                }
            }

            // fallback to user's language code from message
            var userLang = message?.From?.LanguageCode;
            var userCulture = NormalizeLanguageCode(userLang);
            return GetStringForCulture(resourceKey, userCulture);
        }

        private string GetStringForCulture(string resourceKey, string? culture)
        {
            var cacheKey = $"{culture}|{resourceKey}";
            return _cache.GetOrAdd(cacheKey, _ =>
            {
                try
                {
                    var ci = new CultureInfo(culture);
                    var s = _rm.GetString(resourceKey, ci);
                    if (!string.IsNullOrEmpty(s)) return s;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "[LocalizationUtil]: ResourceManager.GetString failed for key={Key} culture={Culture}", resourceKey, culture);
                }

                try
                {
                    var inv = _rm.GetString(resourceKey, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(inv)) return inv;
                }
                catch { }

                return resourceKey;
            });
        }

        private static string NormalizeLanguageCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            var c = code.ToLowerInvariant();
            if (c == "ua") return "uk";
            if (c.Contains('-')) c = c.Split('-')[0];
            return c.Length >= 2 ? c.Substring(0, 2) : string.Empty;
        }
    }
}
