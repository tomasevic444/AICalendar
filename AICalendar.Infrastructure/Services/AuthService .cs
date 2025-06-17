using AICalendar.Application.DTOs.Auth;
using AICalendar.Application.Interfaces;
using AICalendar.Domain; // For UserDocument
using Microsoft.Extensions.Configuration; // For IConfiguration
using Microsoft.IdentityModel.Tokens; // For SymmetricSecurityKey, SigningCredentials
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt; // For JwtSecurityTokenHandler, JwtSecurityToken
using System.Security.Claims; // For ClaimsIdentity, Claim
using System.Text; // For Encoding
using System.Threading.Tasks;
using BCryptNet = BCrypt.Net.BCrypt; // Alias for clarity

namespace AICalendar.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration; // To read JWT settings

        public AuthService(IUserService userService, IConfiguration configuration)
        {
            _userService = userService;
            _configuration = configuration;
        }

        public async Task<(LoginResponseDto? LoginResponse, string? ErrorMessage)> LoginAsync(LoginRequestDto loginDto)
        {
            var user = await _userService.GetUserByUsernameAsync(loginDto.Username);

            if (user == null || !BCryptNet.Verify(loginDto.Password, user.PasswordHash))
            {
                return (null, "Invalid username or password.");
            }

            // User is authenticated, generate JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("JWT SecretKey not configured in appsettings.json"));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                      new Claim(ClaimTypes.NameIdentifier, user.Id), // Standard claim for User ID
                      new Claim(ClaimTypes.Name, user.Username),     // Standard claim for Username
                      new Claim(JwtRegisteredClaimNames.Sub, user.Id), // Subject, often user id
                      new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // JWT ID, unique per token
                      new Claim(JwtRegisteredClaimNames.Email, user.Email), // If email is in UserDocument
                      // Add other claims like roles here: new Claim(ClaimTypes.Role, "Admin")
                  }),
                Expires = DateTime.UtcNow.AddHours(Convert.ToDouble(
                    _configuration["JwtSettings:ExpirationHours"] ?? "1")), // Token expiration
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return (new LoginResponseDto
            {
                Token = tokenString,
                Expiration = tokenDescriptor.Expires.Value,
                UserId = user.Id,
                Username = user.Username
            }, null);
        }
    }
}
