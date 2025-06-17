using AICalendar.Application.DTOs.Event;
using AICalendar.Application.DTOs.Participant;
using AICalendar.Application.Interfaces;
using AICalendar.Domain;
using AICalendar.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AICalendar.Infrastructure.Services
{
    public class EventParticipantService : IEventParticipantService
    {
        private readonly CalendarMongoDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly ILogger<EventParticipantService> _logger; // Good to have for logging

        public EventParticipantService(CalendarMongoDbContext dbContext, IUserService userService, ILogger<EventParticipantService> logger)
        {
            _dbContext = dbContext;
            _userService = userService;
            _logger = logger;
        }

        // --- Implementation of GetEventParticipantsAsync (moved from EventService) ---
        public async Task<IEnumerable<EventParticipantResponseDto>> GetEventParticipantsAsync(string eventId, string requestingUserId)
        {
            var eventDocument = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventDocument == null)
            {
                _logger.LogWarning("Attempted to get participants for non-existent event {EventId}", eventId);
                return Enumerable.Empty<EventParticipantResponseDto>(); // Event not found
            }

            // Authorization: Check if the requesting user is the owner or one of the participants
            bool isOwner = eventDocument.OwnerUserId == requestingUserId;
            bool isParticipant = await _dbContext.EventParticipants
                                    .Find(p => p.EventId == eventId && p.UserId == requestingUserId)
                                    .AnyAsync();

            if (!isOwner && !isParticipant)
            {
                _logger.LogWarning("User {RequestingUserId} unauthorized to view participants for event {EventId}", requestingUserId, eventId);
                return Enumerable.Empty<EventParticipantResponseDto>(); // Or throw an exception for 403 in controller
            }

            var participantDocs = await _dbContext.EventParticipants.Find(p => p.EventId == eventId).ToListAsync();
            var responseList = new List<EventParticipantResponseDto>();

            foreach (var doc in participantDocs)
            {
                var user = await _userService.GetUserByIdAsync(doc.UserId); // Get full UserDocument
                responseList.Add(new EventParticipantResponseDto
                {
                    EventId = doc.EventId,
                    UserId = doc.UserId,
                    Username = user?.Username ?? "Unknown User", // Use Username from UserDocument
                    Status = doc.Status,
                    AddedAtUtc = doc.AddedAtUtc
                });
            }
            return responseList;
        }


        // --- Stubs for other methods (we'll implement these next) ---
        public async Task<(EventParticipantResponseDto? Participant, string? ErrorMessage)> AddParticipantToEventAsync(string eventId, AddParticipantRequestDto addParticipantDto, string requestingUserId)
        {
            // TODO: Implement logic
            // 1. Check if event exists
            // 2. Authorization: Check if requestingUserId is owner of the event
            // 3. Check if user to add exists
            // 4. Check if user is already a participant
            // 5. Create EventParticipantDocument, set status (e.g., "Invited")
            // 6. Save and map to EventParticipantResponseDto
            await Task.CompletedTask;
            return (null, "AddParticipantToEventAsync not implemented yet.");
        }

        public async Task<(bool Success, string? ErrorMessage, EventParticipantResponseDto? UpdatedParticipant)> UpdateParticipantStatusAsync(string eventId, string participantUserIdToUpdate, UpdateParticipantStatusRequestDto updateStatusDto, string requestingUserId)
        {
            // TODO: Implement logic
            // 1. Check if event exists
            // 2. Check if participantUserIdToUpdate is actually a participant of eventId
            // 3. Authorization:
            //    - Event owner can update status of any participant.
            //    - A participant can update their own status (e.g., RSVP Accepted/Declined).
            // 4. Update status in EventParticipantDocument
            // 5. Save and map to EventParticipantResponseDto
            await Task.CompletedTask;
            return (false, "UpdateParticipantStatusAsync not implemented yet.", null);
        }

        public async Task<(bool Success, string? ErrorMessage)> RemoveParticipantFromEventAsync(string eventId, string participantUserIdToRemove, string requestingUserId)
        {
            // TODO: Implement logic
            // 1. Check if event exists
            // 2. Check if participantUserIdToRemove is actually a participant
            // 3. Authorization:
            //    - Event owner can remove any participant (except perhaps themselves if they are the last one? Or handle owner transfer).
            //    - A participant can remove themselves (leave event).
            // 4. Delete EventParticipantDocument
            await Task.CompletedTask;
            return (false, "RemoveParticipantFromEventAsync not implemented yet.");
        }
    }
}