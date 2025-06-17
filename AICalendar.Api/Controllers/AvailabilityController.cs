using AICalendar.Application.DTOs.Availability;
using AICalendar.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims; // If needed for context, though request DTO has participants
using System.Threading.Tasks;

namespace AICalendar.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/slots")] // Using "/slots" as per implied LLM tool endpoint
    [Authorize] // Finding slots usually requires knowing who is asking, or for whom
    public class AvailabilityController : ControllerBase
    {
        private readonly ISlotFindingService _slotFindingService;
        private readonly ILogger<AvailabilityController> _logger;

        public AvailabilityController(ISlotFindingService slotFindingService, ILogger<AvailabilityController> logger)
        {
            _slotFindingService = slotFindingService;
            _logger = logger;
        }

        // Helper to get current user ID if needed for any default participant logic
        private string GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("User ID not found in token claims for an authorized request within AvailabilityController.");
                throw new InvalidOperationException("User ID not found in token claims.");
            }
            return userId;
        }

        // POST /api/v1/slots/find
        [HttpPost("find")]
        [ProducesResponseType(typeof(IEnumerable<AvailableSlotDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> FindAvailableSlots([FromBody] FindSlotsRequestDto requestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Optional: If the requestor should always be included in the participant list
            // var currentUserId = GetCurrentUserId();
            // if (!requestDto.ParticipantUserIds.Contains(currentUserId))
            // {
            //     requestDto.ParticipantUserIds.Add(currentUserId);
            // }

            // Basic validation for the request DTO (some is done by model binding and attributes)
            if (requestDto.SearchWindowEndUtc <= requestDto.SearchWindowStartUtc)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid Search Window", Detail = "Search window end must be after start." });
            }
            if (requestDto.MeetingDurationMinutes <= 0)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid Duration", Detail = "Meeting duration must be a positive number of minutes." });
            }
            if (requestDto.ParticipantUserIds == null || !requestDto.ParticipantUserIds.Any())
            {
                return BadRequest(new ProblemDetails { Title = "Missing Participants", Detail = "At least one participant User ID must be provided." });
            }


            _logger.LogInformation("Finding available slots for {ParticipantCount} users, duration {DurationMinutes} mins, window {Start} to {End}",
                requestDto.ParticipantUserIds.Count, requestDto.MeetingDurationMinutes, requestDto.SearchWindowStartUtc, requestDto.SearchWindowEndUtc);

            var availableSlots = await _slotFindingService.FindAvailableSlotsAsync(requestDto);

            if (!availableSlots.Any())
            {
                _logger.LogInformation("No available slots found for the given criteria.");
                // Return 200 OK with an empty list, or 404 Not Found if "no slots" is considered "resource not found"
                // For availability searches, 200 OK with empty list is common.
                return Ok(Enumerable.Empty<AvailableSlotDto>());
            }

            return Ok(availableSlots);
        }
    }
}