namespace MyUpdatedBot.Core.Models.Entities
{
    public class ChatSettingsEntity
    {
        public long Id { get; set; }
        public long ChatId { get; set; }

        // Feature flags
        public bool SpamProtectionEnabled { get; set; }
        public bool LinksAllowed { get; set; }
    }
}
