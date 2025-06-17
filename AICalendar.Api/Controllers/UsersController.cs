using AICalendar.Application.DTOs.User;
using AICalendar.Application.Interfaces;
using Microsoft.AspNetCore.Authorization; // For [Authorize] and [AllowAnonymous]
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims; // For HttpContext.User
using System.Threading.Tasks;

namespace AICalendar.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")] // If you've set up API versioning
    [Route("api/v{version:apiVersion}/users")]
    // [Authorize] // Default authorization for the whole controller (can be overridden)
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        // Helper method to get current user ID from JWT claims
        // We will use this more once JWT authentication is fully set up in the next major step
        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Or use another claim type if you store user ID differently, e.g., "sub"
        }

        // POST /api/v1/users - Register a new user
        [HttpPost]
        [AllowAnonymous] // Registration should be public
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterUser([FromBody] CreateUserRequestDto createUserDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (userResponse, errorMessage) = await _userService.CreateUserAsync(createUserDto);

            if (errorMessage != null)
            {
                // You might want to distinguish between different types of errors
                // e.g., username taken vs. email taken vs. other validation.
                return BadRequest(new ProblemDetails { Title = "Registration failed", Detail = errorMessage });
            }

            if (userResponse == null) // Should not happen if errorMessage is null, but defensive
            {
                _logger.LogError("User creation returned null userResponse without an error message for username {Username}", createUserDto.Username);
                return BadRequest(new ProblemDetails { Title = "Registration failed", Detail = "An unexpected error occurred." });
            }

            // Return 201 Created with a link to the new resource and the resource itself
            return CreatedAtAction(nameof(GetUserById), new { id = userResponse.Id, version = "1.0" }, userResponse);
        }

        // GET /api/v1/users - Get all users
        [HttpGet]
        [Authorize] // Requires authentication
        [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllUsers()
        {
            // TODO: Add authorization here - e.g., only admins can call this.
            // For now, any authenticated user can.
            // var currentUserId = GetCurrentUserId();
            // if (currentUserId == null) return Unauthorized(); // Should be caught by [Authorize]

            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        // GET /api/v1/users/{id} - Get a specific user by ID
        [HttpGet("{id}")]
        [Authorize] // Requires authentication
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // If user tries to access another user's details without permission
        public async Task<IActionResult> GetUserById(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null) return Unauthorized(); // Should be caught by [Authorize]

            // Authorization: User can get their own details. Admins can get any.
            // if (id != currentUserId /* && !User.IsInRole("Admin") */) // Add Admin role check later
            // {
            //     _logger.LogWarning("User {CurrentUserId} attempted to access user {TargetUserId} without permission.", currentUserId, id);
            //     return Forbid();
            // }

            var user = await _userService.GetUserResponseByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return Ok(user);
        }

        // PUT /api/v1/users/{id} - Update a user
        [HttpPut("{id}")]
        [Authorize] // Requires authentication
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequestDto updateUserDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                // This should ideally be caught by the [Authorize] attribute if JWT is invalid/missing
                return Unauthorized(new ProblemDetails { Title = "Unauthorized", Detail = "User ID not found in token." });
            }

            // The authorization logic (can only update self, or admin can update any)
            // is now inside _userService.UpdateUserAsync
            var (success, errorMessage) = await _userService.UpdateUserAsync(id, updateUserDto, currentUserId);

            if (!success)
            {
                if (errorMessage != null && errorMessage.Contains("Unauthorized"))
                {
                    _logger.LogWarning("User {CurrentUserId} unauthorized to update user {TargetUserId}.", currentUserId, id);
                    return Forbid(); // Or new ObjectResult(new ProblemDetails { Title = "Forbidden", Detail = errorMessage }) { StatusCode = StatusCodes.Status403Forbidden };
                }
                if (errorMessage != null && errorMessage.Contains("not found"))
                {
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = errorMessage });
                }
                return BadRequest(new ProblemDetails { Title = "Update failed", Detail = errorMessage ?? "An error occurred during update." });
            }

            return NoContent(); // Standard response for successful PUT if no content is returned
        }


        // DELETE /api/v1/users/{id} - Delete a user
        [HttpDelete("{id}")]
        [Authorize] // Requires authentication
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new ProblemDetails { Title = "Unauthorized", Detail = "User ID not found in token." });
            }

            // Authorization logic (admin only, or specific conditions) is inside _userService.DeleteUserAsync
            var (success, errorMessage) = await _userService.DeleteUserAsync(id, currentUserId);

            if (!success)
            {
                if (errorMessage != null && errorMessage.Contains("Unauthorized"))
                {
                    _logger.LogWarning("User {CurrentUserId} unauthorized to delete user {TargetUserId}.", currentUserId, id);
                    return Forbid();
                }
                if (errorMessage != null && errorMessage.Contains("not found"))
                {
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = errorMessage });
                }
                if (errorMessage != null && errorMessage.Contains("cannot delete themselves"))
                {
                    return BadRequest(new ProblemDetails { Title = "Operation not allowed", Detail = errorMessage });
                }
                return BadRequest(new ProblemDetails { Title = "Delete failed", Detail = errorMessage ?? "An error occurred during deletion." });
            }

            return NoContent();
        }
    }
}