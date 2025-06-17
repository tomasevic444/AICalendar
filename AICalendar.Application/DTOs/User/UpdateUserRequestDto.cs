using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.DTOs.User
{
    public class UpdateUserRequestDto
    {
        // Users might not want to update username, or it might have special rules
        // For now, let's assume it's not updatable or handled differently.
        // public string? Username { get; set; }

        [EmailAddress]
        public string? Email { get; set; } // Example field to update

        // Password updates should typically be a separate, more secure process
        // (e.g., requiring current password) or a dedicated endpoint.
        // We'll skip password updates via this DTO for now for simplicity.
    }
}
