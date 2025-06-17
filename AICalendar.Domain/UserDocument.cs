using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AICalendar.Domain // Adjust namespace if needed
{
    public class UserDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty; 

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty; 

        [BsonElement("firstName")]
        [BsonIgnoreIfNull] // Store only if provided
        public string? FirstName { get; set; }

        [BsonElement("lastName")]
        [BsonIgnoreIfNull] // Store only if provided
        public string? LastName { get; set; }

        [BsonElement("createdAtUtc")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}