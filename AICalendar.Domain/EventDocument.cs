using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic; 

namespace AICalendar.Domain
{
    public class EventDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("description")]
        [BsonIgnoreIfNull] // Doesn't store the field if the value is null
        public string? Description { get; set; }

        [BsonElement("startTimeUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] // Ensure it's stored as UTC
        public DateTime StartTimeUtc { get; set; }

        [BsonElement("endTimeUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime EndTimeUtc { get; set; }

        [BsonElement("ownerUserId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string OwnerUserId { get; set; } = string.Empty;

        [BsonElement("timeZoneId")]
        [BsonIgnoreIfNull] 
        public string? TimeZoneId { get; set; }

    
    }
}