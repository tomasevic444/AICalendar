using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.DTOs.Participant
{
    public class EventParticipantResponseDto
    {
        public string EventId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty; // To be fetched via IUserService
        public string Status { get; set; } = string.Empty; // e.g., "Invited", "Accepted", "Declined"
        public DateTime AddedAtUtc { get; set; }
    }
}
