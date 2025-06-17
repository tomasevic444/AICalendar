using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.DTOs.Event
{
    public class CreateEventRequestDto
    {
        [Required]
        [MinLength(1)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public DateTime StartTimeUtc { get; set; } // Client should send UTC or be clear about timezone handling

        [Required]
        public DateTime EndTimeUtc { get; set; }

        public string? TimeZoneId { get; set; } 

        // List of User IDs to invite to the event (excluding the owner, who is added automatically)
        public List<string>? ParticipantUserIds { get; set; } = new List<string>();

        // Validation: EndTimeUtc must be after StartTimeUtc
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndTimeUtc <= StartTimeUtc)
            {
                yield return new ValidationResult(
                    "EndTimeUtc must be after StartTimeUtc.",
                    new[] { nameof(EndTimeUtc), nameof(StartTimeUtc) });
            }
        }
    }
}