using AICalendar.Application.DTOs.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.Interfaces
{
    public interface IEventService
    {
        Task<(EventResponseDto? Event, string? ErrorMessage)> CreateEventAsync(CreateEventRequestDto createEventDto, string ownerUserId);
        Task<EventResponseDto?> GetEventByIdAsync(string eventId, string requestingUserId); // requestingUserId for auth checks
        Task<IEnumerable<EventResponseDto>> GetEventsForUserByPeriodAsync(string userId, DateTime startPeriodUtc, DateTime endPeriodUtc);
        Task<(bool Success, string? ErrorMessage, EventResponseDto? UpdatedEvent)> UpdateEventAsync(string eventId, UpdateEventRequestDto updateEventDto, string ownerUserId);
        Task<(bool Success, string? ErrorMessage)> DeleteEventAsync(string eventId, string ownerUserId);
    }
}