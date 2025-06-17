using System.ComponentModel.DataAnnotations;

namespace AICalendar.Application.DTOs.User 
{
    public class UpdateUserRequestDto
    {
        // Username is typically not updated or has special handling.
        // Email updates might require re-verification, so handle with care.
        // For now, let's allow email update.
        [EmailAddress]
        public string? Email { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        // Password updates should be a separate, secure flow.
        // e.g., POST /api/v1/users/{id}/change-password
    }
}