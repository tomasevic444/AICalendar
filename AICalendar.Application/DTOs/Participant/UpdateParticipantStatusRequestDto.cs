using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.DTOs.Participant
{
    public class UpdateParticipantStatusRequestDto
    {
        [Required(ErrorMessage = "Status is required.")]
        public string Status { get; set; } = string.Empty; // New status, e.g., "Accepted", "Declined"
                                                           
    }
}
