using System.ComponentModel.DataAnnotations;

namespace AICalendar.Application.DTOs.User
{
    public class CreateUserRequestDto
    {
        [Required]
        [MinLength(3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(6)] // Example minimum password length
        public string Password { get; set; } = string.Empty;
    }
}
