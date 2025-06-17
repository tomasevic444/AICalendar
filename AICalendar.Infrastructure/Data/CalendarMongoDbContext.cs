using AICalendar.Domain;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;


namespace AICalendar.Infrastructure.Data
{
    public class CalendarMongoDbContext
    {
        private readonly IMongoDatabase _database;
        private readonly MongoDbSettings _settings;

        public CalendarMongoDbContext(IOptions<MongoDbSettings> settings)
        {
            _settings = settings.Value;
            var client = new MongoClient(_settings.ConnectionString);
            _database = client.GetDatabase(_settings.DatabaseName);
        }

        public IMongoCollection<UserDocument> Users =>
            _database.GetCollection<UserDocument>(_settings.UsersCollectionName);

        public IMongoCollection<EventDocument> Events =>
            _database.GetCollection<EventDocument>(_settings.EventsCollectionName);

        public IMongoCollection<EventParticipantDocument> EventParticipants =>
            _database.GetCollection<EventParticipantDocument>(_settings.EventParticipantsCollectionName);

        public async Task CreateIndexesAsync()
        {
            // Example for Users collection: Unique index on Username
            var userUsernameIndexKeysDefinition = Builders<UserDocument>.IndexKeys.Ascending(u => u.Username);
            var userUsernameIndexOptions = new CreateIndexOptions { Unique = true };
            var userUsernameIndexModel = new CreateIndexModel<UserDocument>(userUsernameIndexKeysDefinition, userUsernameIndexOptions);
            await Users.Indexes.CreateOneAsync(userUsernameIndexModel);

            // Example for Events collection: Index on OwnerUserId, StartTimeUtc, EndTimeUtc
            var eventOwnerIndex = Builders<EventDocument>.IndexKeys.Ascending(e => e.OwnerUserId);
            await Events.Indexes.CreateOneAsync(new CreateIndexModel<EventDocument>(eventOwnerIndex));

            var eventTimeIndex = Builders<EventDocument>.IndexKeys.Combine(
                Builders<EventDocument>.IndexKeys.Ascending(e => e.StartTimeUtc),
                Builders<EventDocument>.IndexKeys.Ascending(e => e.EndTimeUtc)
            );
            await Events.Indexes.CreateOneAsync(new CreateIndexModel<EventDocument>(eventTimeIndex));

            var participantEventIdIndex = Builders<EventParticipantDocument>.IndexKeys.Ascending(p => p.EventId);
            await EventParticipants.Indexes.CreateOneAsync(new CreateIndexModel<EventParticipantDocument>(participantEventIdIndex));

            var participantUserIdIndex = Builders<EventParticipantDocument>.IndexKeys.Ascending(p => p.UserId);
            await EventParticipants.Indexes.CreateOneAsync(new CreateIndexModel<EventParticipantDocument>(participantUserIdIndex));

            var participantCompoundIndex = Builders<EventParticipantDocument>.IndexKeys.Combine(
                Builders<EventParticipantDocument>.IndexKeys.Ascending(p => p.EventId),
                Builders<EventParticipantDocument>.IndexKeys.Ascending(p => p.UserId)
            );

            await EventParticipants.Indexes.CreateOneAsync(new CreateIndexModel<EventParticipantDocument>(participantCompoundIndex));


            Console.WriteLine("MongoDB indexes checked/created.");
        }
    }
}