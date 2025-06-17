using AICalendar.Application.DTOs.Auth;
using AICalendar.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AICalendar.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        // POST /api/v1/auth/login
        [HttpPost("login")]
        [AllowAnonymous] // Login endpoint must be public
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (loginResponse, errorMessage) = await _authService.LoginAsync(loginDto);

            if (errorMessage != null)
            {
                // Log the failed login attempt for security monitoring (optional)
                _logger.LogWarning("Login failed for user {Username}: {ErrorMessage}", loginDto.Username, errorMessage);
                return Unauthorized(new ProblemDetails { Title = "Login Failed", Detail = errorMessage });
            }

            if (loginResponse == null) // Should not happen if errorMessage is null
            {
                _logger.LogError("Login returned null response without an error message for user {Username}", loginDto.Username);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Login Error", Detail = "An unexpected error occurred during login." });
            }

            return Ok(loginResponse);
        }
    }
}