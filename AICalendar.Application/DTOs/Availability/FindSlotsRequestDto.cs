using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.DTOs.Availability
{
    public class FindSlotsRequestDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one participant User ID must be provided.")]
        public List<string> ParticipantUserIds { get; set; } = new List<string>();

        [Required]
        public DateTime SearchWindowStartUtc { get; set; } // Start of the period to search within

        [Required]
        public DateTime SearchWindowEndUtc { get; set; }   // End of the period to search within

        [Required]
        [Range(5, 1440, ErrorMessage = "Duration must be between 5 and 1440 minutes (24 hours).")] // Example: 5 min to 1 day
        public int MeetingDurationMinutes { get; set; }

       
    }
}

