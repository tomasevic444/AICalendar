using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.DTOs.Participant
{
    public class AddParticipantRequestDto
    {
        [Required(ErrorMessage = "UserId of the participant to add is required.")]
        public string UserId { get; set; } = string.Empty;


    }
}
