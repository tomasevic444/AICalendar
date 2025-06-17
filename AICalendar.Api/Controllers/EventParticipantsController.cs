using AICalendar.Application.DTOs.Event; // For EventParticipantResponseDto
using AICalendar.Application.DTOs.Participant; // For AddParticipantRequestDto, UpdateParticipantStatusRequestDto
using AICalendar.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AICalendar.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/events/{eventId}/participants")] // Base route for this controller
    [Authorize] // All participant operations require an authenticated user
    public class EventParticipantsController : ControllerBase
    {
        private readonly IEventParticipantService _participantService;
        private readonly ILogger<EventParticipantsController> _logger;

        public EventParticipantsController(IEventParticipantService participantService, ILogger<EventParticipantsController> logger)
        {
            _participantService = participantService;
            _logger = logger;
        }

        private string GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("User ID not found in token claims for an authorized request within EventParticipantsController.");
                throw new InvalidOperationException("User ID not found in token claims.");
            }
            return userId;
        }

        // GET /api/v1/events/{eventId}/participants
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<EventParticipantResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] // If event not found
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] // If not authorized to view
        public async Task<IActionResult> GetParticipants(string eventId)
        {
            var requestingUserId = GetCurrentUserId();
            var participants = await _participantService.GetEventParticipantsAsync(eventId, requestingUserId);

            // The service returns an empty list if event not found or user not authorized.
            // We might want to be more specific with 404 vs 403 based on service return if it provided more detail.
            // For now, if it's empty, it could mean either. If event truly doesn't exist, service handles that.
            // If service threw an exception for "NotFound" or "Forbidden", we'd catch it in a middleware.
            // For now, let's assume if participants has items or is empty due to no participants it's OK.
            // If service indicated "Event not found" explicitly, we could return NotFound.
            // If it indicated "Unauthorized", we could return Forbid().
            // The current service impl for GetEventParticipantsAsync returns empty for "not found" or "unauthorized to view".

            return Ok(participants);
        }

        // POST /api/v1/events/{eventId}/participants
        [HttpPost]
        [ProducesResponseType(typeof(EventParticipantResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] // e.g., user already participant, user not found
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] // e.g., not event owner
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] // e.g., event not found
        public async Task<IActionResult> AddParticipant(string eventId, [FromBody] AddParticipantRequestDto addParticipantDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var requestingUserId = GetCurrentUserId(); // This is the user performing the action (event owner)
            var (participant, errorMessage) = await _participantService.AddParticipantToEventAsync(eventId, addParticipantDto, requestingUserId);

            if (errorMessage != null)
            {
                if (errorMessage.Contains("Event not found"))
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = errorMessage });
                if (errorMessage.Contains("Unauthorized") || errorMessage.Contains("Only the event owner"))
                    return Forbid(); // new ObjectResult(new ProblemDetails { Title = "Forbidden", Detail = errorMessage }) { StatusCode = StatusCodes.Status403Forbidden };
                if (errorMessage.Contains("User not found") || errorMessage.Contains("already a participant")) // User to add not found, or already exists
                    return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = errorMessage });

                return BadRequest(new ProblemDetails { Title = "Failed to add participant", Detail = errorMessage });
            }
            if (participant == null) // Defensive
            {
                _logger.LogError("AddParticipant for event {EventId} returned null response without error message.", eventId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Add Participant Error", Detail = "An unexpected error occurred." });
            }


            // For a POST creating a sub-resource, the location header might point to the specific participant:
            // e.g., /api/v1/events/{eventId}/participants/{userIdOfAddedParticipant}
            return CreatedAtAction(nameof(GetParticipantById), // We'll need a GetParticipantById action for this
                                new { eventId = eventId, userId = participant.UserId, version = "1.0" },
                                participant);
        }

        // Helper action for CreatedAtAction (not directly in API spec, but good for RESTful POST responses)
        // GET /api/v1/events/{eventId}/participants/{userId}
        [HttpGet("{userId}", Name = "GetParticipantById")] // Give it a route name
        [ProducesResponseType(typeof(EventParticipantResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetParticipantById(string eventId, string userId)
        {
            var requestingUserId = GetCurrentUserId();
            // We need a service method like: GetSingleParticipantForEventAsync(eventId, participantIdToGet, requestingUserId)
            // For now, let's filter from the list. This is inefficient for a single get but works for CreatedAtAction.
            var participants = await _participantService.GetEventParticipantsAsync(eventId, requestingUserId);
            var participant = participants.FirstOrDefault(p => p.UserId == userId);

            if (participant == null)
            {
                // Could be event not found, participant not in event, or user not authorized to see participants
                return NotFound(new ProblemDetails { Title = "Not Found", Detail = "Participant not found or event not accessible." });
            }
            return Ok(participant);
        }


        // PUT /api/v1/events/{eventId}/participants/{userId}
        [HttpPut("{userId}")]
        [ProducesResponseType(typeof(EventParticipantResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] // e.g., invalid status
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] // event or participant not found
        public async Task<IActionResult> UpdateParticipantStatus(string eventId, string userId, [FromBody] UpdateParticipantStatusRequestDto updateStatusDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var requestingUserId = GetCurrentUserId();
            var (success, errorMessage, updatedParticipant) = await _participantService.UpdateParticipantStatusAsync(eventId, userId, updateStatusDto, requestingUserId);

            if (!success)
            {
                if (errorMessage != null && errorMessage.Contains("Event not found"))
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = errorMessage });
                if (errorMessage != null && errorMessage.Contains("Participant with User ID") && errorMessage.Contains("not found"))
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = errorMessage });
                if (errorMessage != null && errorMessage.Contains("Unauthorized"))
                    return Forbid();
                if (errorMessage != null && errorMessage.Contains("Invalid status"))
                    return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = errorMessage });
                if (errorMessage != null && errorMessage.Contains("owner's status is fixed"))
                    return BadRequest(new ProblemDetails { Title = "Operation Not Allowed", Detail = errorMessage });

                return BadRequest(new ProblemDetails { Title = "Failed to update participant status", Detail = errorMessage ?? "An error occurred." });
            }
            if (updatedParticipant == null && success)  // Should ideally not happen if success is true and no error
            {
                _logger.LogWarning("UpdateParticipantStatus for event {EventId}, participant {ParticipantId} reported success but returned null updatedParticipant. Message: {ErrorMessage}", eventId, userId, errorMessage);
                // If no changes were made and the service indicated this
                if (errorMessage != null && (errorMessage.Contains("No update needed") || errorMessage.Contains("already set")))
                {
                    // Re-fetch the current state of the participant
                    var participants = await _participantService.GetEventParticipantsAsync(eventId, requestingUserId);
                    var currentParticipantState = participants.FirstOrDefault(p => p.UserId == userId);
                    return Ok(currentParticipantState);
                }
                return Ok(); // Or a generic success if we can't get the participant DTO easily
            }

            return Ok(updatedParticipant);
        }

        // DELETE /api/v1/events/{eventId}/participants/{userId}
        [HttpDelete("{userId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] // event or participant not found
        public async Task<IActionResult> RemoveParticipant(string eventId, string userId)
        {
            var requestingUserId = GetCurrentUserId();
            var (success, errorMessage) = await _participantService.RemoveParticipantFromEventAsync(eventId, userId, requestingUserId);

            if (!success)
            {
                if (errorMessage != null && errorMessage.Contains("Event not found"))
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = errorMessage });
                if (errorMessage != null && errorMessage.Contains("Participant with User ID") && errorMessage.Contains("not found"))
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = errorMessage });
                if (errorMessage != null && (errorMessage.Contains("Unauthorized") || errorMessage.Contains("owner cannot remove themselves")))
                    return Forbid(); // Or BadRequest for "owner cannot remove..."

                return BadRequest(new ProblemDetails { Title = "Failed to remove participant", Detail = errorMessage ?? "An error occurred." });
            }

            return NoContent();
        }
    }
}