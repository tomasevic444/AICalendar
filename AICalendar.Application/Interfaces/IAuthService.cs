using AICalendar.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.Interfaces
{
    public interface IAuthService
    {
        Task<(LoginResponseDto? LoginResponse, string? ErrorMessage)> LoginAsync(LoginRequestDto loginDto);
    }
}
