using AICalendar.Application.DTOs.Event;
using AICalendar.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq; 
using System.Security.Claims;
using System.Threading.Tasks;

namespace AICalendar.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/events")]
    [Authorize] 
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;
        private readonly ILogger<EventsController> _logger;

        public EventsController(IEventService eventService, ILogger<EventsController> logger)
        {
            _eventService = eventService;
            _logger = logger;
        }

        private string GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {

                _logger.LogError("User ID not found in token claims for an authorized request.");
                throw new InvalidOperationException("User ID not found in token claims.");
            }
            return userId;
        }

        // POST /api/v1/events
        [HttpPost]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequestDto createEventDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ownerUserId = GetCurrentUserId();
            var (eventResponse, errorMessage) = await _eventService.CreateEventAsync(createEventDto, ownerUserId);

            if (errorMessage != null)
            {
                return BadRequest(new ProblemDetails { Title = "Event creation failed", Detail = errorMessage });
            }
            if (eventResponse == null) // Defensive
            {
                _logger.LogError("Event creation returned null response without error message for user {UserId}", ownerUserId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Event Creation Error", Detail = "An unexpected error occurred." });
            }


            return CreatedAtAction(nameof(GetEventById), new { id = eventResponse.Id, version = "1.0" }, eventResponse);
        }

        // GET /api/v1/events?startPeriodUtc=2024-01-01T00:00:00Z&endPeriodUtc=2024-01-31T23:59:59Z (optional params)
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<EventResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] // For invalid period if provided
        public async Task<IActionResult> GetEventsForUser([FromQuery] DateTime? startPeriodUtc, [FromQuery] DateTime? endPeriodUtc)
        {
            // Validate period only if both parameters are provided
            if (startPeriodUtc.HasValue && endPeriodUtc.HasValue)
            {
                if (endPeriodUtc.Value <= startPeriodUtc.Value)
                {
                    return BadRequest(new ProblemDetails { Title = "Invalid period", Detail = "endPeriodUtc must be after startPeriodUtc." });
                }
            }
            else if (startPeriodUtc.HasValue != endPeriodUtc.HasValue) // XOR: one is provided but not the other
            {
                return BadRequest(new ProblemDetails { Title = "Invalid period parameters", Detail = "If providing a period, both startPeriodUtc and endPeriodUtc must be supplied." });
            }

            var userId = GetCurrentUserId();
            // Call the renamed service method
            var events = await _eventService.GetEventsForUserAsync(userId, startPeriodUtc, endPeriodUtc);
            return Ok(events);
        }

        // GET /api/v1/events/{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        // 403 Forbidden is handled implicitly by GetEventByIdAsync returning null if not authorized
        public async Task<IActionResult> GetEventById(string id)
        {
            var requestingUserId = GetCurrentUserId();
            var eventResponse = await _eventService.GetEventByIdAsync(id, requestingUserId);

            if (eventResponse == null)
            {
                // This could be "Not Found" or "Forbidden" depending on service logic.
                // The service currently returns null for both.
                return NotFound(new ProblemDetails { Title = "Not Found", Detail = "Event not found or you do not have permission to view it." });
            }
            return Ok(eventResponse);
        }

        // PUT /api/v1/events/{id}
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)] // Returning the updated event
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEvent(string id, [FromBody] UpdateEventRequestDto updateEventDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ownerUserId = GetCurrentUserId(); // Or requestingUserId, service layer enforces ownership
            var (success, errorMessage, updatedEvent) = await _eventService.UpdateEventAsync(id, updateEventDto, ownerUserId);

            if (!success)
            {
                if (errorMessage != null && errorMessage.Contains("Unauthorized"))
                {
                    return Forbid(); // new ObjectResult(new ProblemDetails { Title = "Forbidden", Detail = errorMessage }) { StatusCode = StatusCodes.Status403Forbidden };
                }
                if (errorMessage != null && errorMessage.Contains("not found"))
                {
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = errorMessage });
                }
                return BadRequest(new ProblemDetails { Title = "Update failed", Detail = errorMessage ?? "An error occurred during update." });
            }
            if (updatedEvent == null && success) // Should not happen if success is true and no error
            {
                _logger.LogWarning("UpdateEvent for {EventId} reported success but returned null updatedEvent. Message: {ErrorMessage}", id, errorMessage);
                // If no changes were made and the service indicated this, NoContent might be appropriate
                if (errorMessage != null && errorMessage.Contains("No effective changes"))
                {
                    return Ok(await _eventService.GetEventByIdAsync(id, ownerUserId)); // Return current state
                }
                return Ok(); // Or NoContent() if "no changes made" is a common scenario for 204
            }


            return Ok(updatedEvent); // Return the updated event
        }

        // DELETE /api/v1/events/{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEvent(string id)
        {
            var ownerUserId = GetCurrentUserId(); // Or requestingUserId, service layer enforces ownership
            var (success, errorMessage) = await _eventService.DeleteEventAsync(id, ownerUserId);

            if (!success)
            {
                if (errorMessage != null && errorMessage.Contains("Unauthorized"))
                {
                    return Forbid();
                }
                if (errorMessage != null && errorMessage.Contains("not found"))
                {
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = errorMessage });
                }
                return BadRequest(new ProblemDetails { Title = "Delete failed", Detail = errorMessage ?? "An error occurred during deletion." });
            }

            return NoContent();
        }
    }
}