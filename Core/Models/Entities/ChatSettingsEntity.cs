using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
