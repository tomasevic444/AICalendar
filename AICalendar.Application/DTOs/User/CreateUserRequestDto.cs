using System.ComponentModel.DataAnnotations;

namespace AICalendar.Application.DTOs.User 
{
    public class CreateUserRequestDto
    {
        [Required]
        [MinLength(3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public string? FirstName { get; set; } 

        public string? LastName { get; set; }  
    }
}