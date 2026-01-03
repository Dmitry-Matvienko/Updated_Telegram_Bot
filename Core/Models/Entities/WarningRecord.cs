
using System.ComponentModel.DataAnnotations;

namespace MyUpdatedBot.Core.Models.Entities
{
    public class WarningRecord
    {
        public long Id { get; set; }
        public long ChatId { get; set; }
        public long UserRefId { get; set; }
        public UserEntity? User { get; set; }
        public int WarningsCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
