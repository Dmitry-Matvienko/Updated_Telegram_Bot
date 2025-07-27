using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyUpdatedBot.Core.Models.Entities
{
    public class MessageCountEntity
    {
        [Key]
        public long Id { get; set; }
        public long UserRefId { get; set; }    // FK for Users.Id
        public UserEntity User { get; set; } = null!;
        public long ChatId { get; set; }
        public string ?Rank { get; set; }
        public int MessageCount { get; set; }
    }
}
