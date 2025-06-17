using AICalendar.Application.DTOs.Event; // For EventParticipantResponseDto
using AICalendar.Application.DTOs.Participant; // For AddParticipantRequestDto, UpdateParticipantStatusRequestDto
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AICalendar.Application.Interfaces
{
    public interface IEventParticipantService
    {
        Task<IEnumerable<EventParticipantResponseDto>> GetEventParticipantsAsync(string eventId, string requestingUserId);
        Task<(EventParticipantResponseDto? Participant, string? ErrorMessage)> AddParticipantToEventAsync(string eventId, AddParticipantRequestDto addParticipantDto, string requestingUserId);
        Task<(bool Success, string? ErrorMessage, EventParticipantResponseDto? UpdatedParticipant)> UpdateParticipantStatusAsync(string eventId, string participantUserIdToUpdate, UpdateParticipantStatusRequestDto updateStatusDto, string requestingUserId);
        Task<(bool Success, string? ErrorMessage)> RemoveParticipantFromEventAsync(string eventId, string participantUserIdToRemove, string requestingUserId);
    }
}