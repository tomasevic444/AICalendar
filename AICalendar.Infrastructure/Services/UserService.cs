using AICalendar.Application.DTOs;
using AICalendar.Application.DTOs.User;
using AICalendar.Application.Interfaces;
using AICalendar.Domain;
using AICalendar.Infrastructure.Data;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCryptNet = BCrypt.Net.BCrypt; // Alias for clarity

namespace AICalendar.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IMongoCollection<UserDocument> _usersCollection;

        public UserService(CalendarMongoDbContext dbContext)
        {
            _usersCollection = dbContext.Users;
        }

        public async Task<(UserResponseDto? User, string? ErrorMessage)> CreateUserAsync(CreateUserRequestDto createUserDto)
        {
            // Check for existing username
            var existingUserByUsername = await _usersCollection.Find(u => u.Username == createUserDto.Username).FirstOrDefaultAsync();
            if (existingUserByUsername != null)
            {
                return (null, "Username already exists.");
            }

            // Check for existing email 
            var existingUserByEmail = await _usersCollection.Find(u => u.Email == createUserDto.Email).FirstOrDefaultAsync();
            if (existingUserByEmail != null)
            {
                return (null, "Email address is already in use.");
            }

            var newUser = new UserDocument
            {
                Username = createUserDto.Username,
                Email = createUserDto.Email,
                PasswordHash = BCryptNet.HashPassword(createUserDto.Password),
                FirstName = createUserDto.FirstName, // Will be null if not provided
                LastName = createUserDto.LastName,   // Will be null if not provided
                CreatedAtUtc = DateTime.UtcNow
            };

            await _usersCollection.InsertOneAsync(newUser);

            // Ensure newUser.Id is populated (MongoDB driver does this)
            var userResponse = new UserResponseDto
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Email = newUser.Email,
                FirstName = newUser.FirstName,
                LastName = newUser.LastName
            };
            return (userResponse, null);
        }

        public async Task<UserDocument?> GetUserByIdAsync(string userId)
        {
            return await _usersCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();
        }

        public async Task<UserResponseDto?> GetUserResponseByIdAsync(string userId)
        {
            var user = await GetUserByIdAsync(userId); // GetUserByIdAsync fetches the full UserDocument
            if (user == null) return null;
            return new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }


        public async Task<UserDocument?> GetUserByUsernameAsync(string username)
        {
            return await _usersCollection.Find(u => u.Username == username).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync()
        {
            var users = await _usersCollection.Find(_ => true).ToListAsync();
            return users.Select(user => new UserResponseDto // Use 'user' as lambda param
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            });
        }
        public async Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(string userIdToUpdate, UpdateUserRequestDto updateUserDto, string currentUserId)
        {
            if (userIdToUpdate != currentUserId /* && !currentUserIsAdmin */)
            {
                return (false, "Unauthorized to update this user.");
            }

            var user = await GetUserByIdAsync(userIdToUpdate);
            if (user == null)
            {
                return (false, "User not found.");
            }

            var updateOperations = new List<UpdateDefinition<UserDocument>>();

            // Email Update - consider uniqueness check if email must be unique
            if (updateUserDto.Email != null && user.Email != updateUserDto.Email)
            {
                // Optional: Check if the new email is already taken by another user
                var emailTaken = await _usersCollection.Find(u => u.Id != userIdToUpdate && u.Email == updateUserDto.Email).AnyAsync();
                if (emailTaken)
                {
                    return (false, "New email address is already in use by another account.");
                }
                updateOperations.Add(Builders<UserDocument>.Update.Set(u => u.Email, updateUserDto.Email));
            }

            // FirstName Update
            if (updateUserDto.FirstName != user.FirstName) // Handles null to value, value to null, value to different value
            {
                updateOperations.Add(Builders<UserDocument>.Update.Set(u => u.FirstName, updateUserDto.FirstName));
            }

            // LastName Update
            if (updateUserDto.LastName != user.LastName)
            {
                updateOperations.Add(Builders<UserDocument>.Update.Set(u => u.LastName, updateUserDto.LastName));
            }

            if (!updateOperations.Any())
            {
                return (true, "No updatable changes provided or values are the same.");
            }

            var combinedUpdateDefinition = Builders<UserDocument>.Update.Combine(updateOperations);
            var result = await _usersCollection.UpdateOneAsync(u => u.Id == userIdToUpdate, combinedUpdateDefinition);

            if (result.IsAcknowledged)
            {
                if (result.ModifiedCount > 0)
                {
                    return (true, null); // Success
                }
                else if (result.MatchedCount > 0 && result.ModifiedCount == 0)
                {
                    return (true, "User found, but no effective changes were made (values might be the same).");
                }
                else // MatchedCount == 0
                {
                    return (false, "User not found (should have been caught, defensive).");
                }
            }
            return (false, "Update failed (not acknowledged by database).");
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(string userIdToDelete, string currentUserId)
        {
            if (userIdToDelete == currentUserId)
            {
                return (false, "Users cannot delete themselves through this operation.");
            }


            var result = await _usersCollection.DeleteOneAsync(u => u.Id == userIdToDelete);
            if (result.IsAcknowledged && result.DeletedCount > 0)
            {
                return (true, null);
            }
            return (false, "User not found or delete failed.");
        }
    }
}

