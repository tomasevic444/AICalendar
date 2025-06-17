using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Infrastructure.Data
{
    public class MongoDbSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
        public string UsersCollectionName { get; set; } = "users";
        public string EventsCollectionName { get; set; } = "events";
        public string EventParticipantsCollectionName { get; set; } = "eventParticipants";
    }
}

