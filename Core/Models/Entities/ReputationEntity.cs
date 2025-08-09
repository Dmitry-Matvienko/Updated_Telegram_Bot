namespace MyUpdatedBot.Core.Models.Entities
{
    public class ReputationEntity
    {
        public int Id { get; set; }
        public long UserRefId { get; set; }    // FK for Users.Id
        public UserEntity User { get; set; } = null!;
        public long ChatId { get; set; }
        public int Rating { get; set; } // TODO: Rename to Reputation and update DB
        public DateTime LastGiven { get; set; } = DateTime.UtcNow;
    }
}