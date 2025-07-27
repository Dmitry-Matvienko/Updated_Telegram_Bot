using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyUpdatedBot.Core.Models.Entities
{
    [Index(nameof(UserId), IsUnique = true)]
    public class UserEntity
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string? FirstName { get; set; }
        public string? Username { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<MessageCountEntity> MessageStats { get; set; }
            = new List<MessageCountEntity>();
    }
}
