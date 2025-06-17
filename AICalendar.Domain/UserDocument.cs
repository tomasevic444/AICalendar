using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace AICalendar.Domain
{
    public class UserDocument
    {
        [BsonId] // Marks this property as the document's primary key
        [BsonRepresentation(BsonType.ObjectId)] // Tells MongoDB to store it as an ObjectId and represent as string in C#
        public string Id { get; set; } = string.Empty; 

        [BsonElement("username")] 
        public string Username { get; set; } = string.Empty;

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

    }
}

