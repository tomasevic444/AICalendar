using AICalendar.Application.DTOs.User;
using AICalendar.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.Interfaces
{
    public interface IUserService
    {
        Task<(UserResponseDto? User, string? ErrorMessage)> CreateUserAsync(CreateUserRequestDto createUserDto);
        Task<UserDocument?> GetUserByIdAsync(string userId); // Returns the document for internal use by Auth or other services
        Task<UserResponseDto?> GetUserResponseByIdAsync(string userId); // Returns DTO for API responses
        Task<UserDocument?> GetUserByUsernameAsync(string username); // For login
        Task<IEnumerable<UserResponseDto>> GetAllUsersAsync(/* Add filtering/pagination params later if needed */);
       // Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(string userId, UpdateUserRequestDto updateUserDto, string currentUserId); // currentUserId for auth checks
        Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(string userIdToDelete, string currentUserId); // currentUserId for auth checks
                                                                                                                 // bool VerifyPassword(string password, string passwordHash); // This might be better internal to the implementation or an Auth service
    }
}

