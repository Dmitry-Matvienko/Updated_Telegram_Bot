using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyUpdatedBot.Core.Models.Entities
{
    public class ReputationGivenEntity
    {
        public int Id { get; set; }
        public long FromUserId { get; set; }   // Telegram user id of the one who gave
        public long ToUserRefId { get; set; }  // FK - Users.Id (UserEntity.Id)
        public long ChatId { get; set; }

        public DateTime LastGiven { get; set; } = DateTime.UtcNow;
    }
}
