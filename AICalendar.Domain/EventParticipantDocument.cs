using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace AICalendar.Domain
{
    public class EventParticipantDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("eventId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string EventId { get; set; } = string.Empty;

        [BsonElement("userId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("status")]
        public string Status { get; set; } = "Invited"; // e.g., Invited, Accepted, Declined, Tentative

        [BsonElement("addedAtUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
