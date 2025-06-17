using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.DTOs.Event
{
    public class EventParticipantDetailsDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty; 
        public string Status { get; set; } = string.Empty; 
    }
}
