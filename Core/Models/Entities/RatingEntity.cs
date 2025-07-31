namespace MyUpdatedBot.Core.Models.Entities
{
    public class RatingEntity
    {
        public int Id { get; set; }
        public long UserRefId { get; set; }    // FK for Users.Id
        public UserEntity User { get; set; } = null!;
        public long ChatId { get; set; }
        public int Rating { get; set; }
        public DateTime LastGiven { get; set; } = DateTime.UtcNow;
    }
}