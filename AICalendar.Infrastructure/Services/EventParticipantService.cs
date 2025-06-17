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


        public async Task<(EventParticipantResponseDto? Participant, string? ErrorMessage)> AddParticipantToEventAsync(
    string eventId, AddParticipantRequestDto addParticipantDto, string requestingUserId)
        {
            // 1. Check if event exists
            var eventDocument = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventDocument == null)
            {
                _logger.LogWarning("AddParticipant: Event {EventId} not found.", eventId);
                return (null, "Event not found.");
            }

            // 2. Authorization: Only the event owner can add participants
            if (eventDocument.OwnerUserId != requestingUserId)
            {
                _logger.LogWarning("AddParticipant: User {RequestingUserId} is not owner of event {EventId}. Owner is {OwnerUserId}.",
                    requestingUserId, eventId, eventDocument.OwnerUserId);
                return (null, "Unauthorized. Only the event owner can add participants.");
            }

            // 3. Check if the user to be added exists
            var userToAdd = await _userService.GetUserByIdAsync(addParticipantDto.UserId);
            if (userToAdd == null)
            {
                _logger.LogWarning("AddParticipant: User to add {UserIdToAdd} not found for event {EventId}.", addParticipantDto.UserId, eventId);
                return (null, $"User with ID '{addParticipantDto.UserId}' not found.");
            }

            // Edge case: Cannot add the owner as a participant again (owner is implicitly a participant)
            if (addParticipantDto.UserId == eventDocument.OwnerUserId)
            {
                _logger.LogInformation("AddParticipant: Attempt to add owner {OwnerUserId} as participant to event {EventId}. Owner is already considered a participant.", eventDocument.OwnerUserId, eventId);
                // Depending on desired behavior, you could return the owner's existing participant details
                // or a specific message. For now, let's prevent re-adding.
                return (null, "The event owner cannot be added as a separate participant.");
            }

            // 4. Check if user is already a participant
            var existingParticipant = await _dbContext.EventParticipants
                .Find(p => p.EventId == eventId && p.UserId == addParticipantDto.UserId)
                .FirstOrDefaultAsync();

            if (existingParticipant != null)
            {
                _logger.LogInformation("AddParticipant: User {UserIdToAdd} is already a participant in event {EventId} with status {Status}.",
                    addParticipantDto.UserId, eventId, existingParticipant.Status);
                // Return existing participant details
                return (new EventParticipantResponseDto
                {
                    EventId = existingParticipant.EventId,
                    UserId = existingParticipant.UserId,
                    Username = userToAdd.Username, // We already fetched userToAdd
                    Status = existingParticipant.Status,
                    AddedAtUtc = existingParticipant.AddedAtUtc
                }, "User is already a participant in this event.");
            }

            // 5. Create new EventParticipantDocument
            var newParticipantDocument = new EventParticipantDocument
            {
                EventId = eventId,
                UserId = addParticipantDto.UserId,
                Status = "Invited", // Default status when adding a new participant
                AddedAtUtc = DateTime.UtcNow
                // Id will be generated by MongoDB
            };

            // 6. Save to database
            await _dbContext.EventParticipants.InsertOneAsync(newParticipantDocument);
            _logger.LogInformation("AddParticipant: User {UserIdToAdd} successfully added to event {EventId} by owner {RequestingUserId}.",
                addParticipantDto.UserId, eventId, requestingUserId);


            // 7. Return details of the newly added participant
            return (new EventParticipantResponseDto
            {
                EventId = newParticipantDocument.EventId,
                UserId = newParticipantDocument.UserId,
                Username = userToAdd.Username, // We already fetched userToAdd
                Status = newParticipantDocument.Status,
                AddedAtUtc = newParticipantDocument.AddedAtUtc
            }, null);
        }

        public async Task<(bool Success, string? ErrorMessage, EventParticipantResponseDto? UpdatedParticipant)> UpdateParticipantStatusAsync(
    string eventId, string participantUserIdToUpdate, UpdateParticipantStatusRequestDto updateStatusDto, string requestingUserId)
        {
            // 1. Validate the new status value (basic example)
            var allowedStatuses = new List<string> { "Invited", "Accepted", "Declined", "Tentative" }; // Define your valid statuses
            if (string.IsNullOrWhiteSpace(updateStatusDto.Status) || !allowedStatuses.Contains(updateStatusDto.Status, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("UpdateParticipantStatus: Invalid status '{NewStatus}' provided for event {EventId}, participant {ParticipantUserId}.",
                    updateStatusDto.Status, eventId, participantUserIdToUpdate);
                return (false, $"Invalid status provided. Allowed statuses are: {string.Join(", ", allowedStatuses)}.", null);
            }
            var normalizedNewStatus = allowedStatuses.First(s => s.Equals(updateStatusDto.Status, StringComparison.OrdinalIgnoreCase)); // Use a consistent case

            // 2. Check if event exists
            var eventDocument = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventDocument == null)
            {
                _logger.LogWarning("UpdateParticipantStatus: Event {EventId} not found.", eventId);
                return (false, "Event not found.", null);
            }

            // 3. Check if the participant record exists
            var participantDocument = await _dbContext.EventParticipants
                .Find(p => p.EventId == eventId && p.UserId == participantUserIdToUpdate)
                .FirstOrDefaultAsync();

            if (participantDocument == null)
            {
                _logger.LogWarning("UpdateParticipantStatus: Participant {ParticipantUserId} not found for event {EventId}.",
                    participantUserIdToUpdate, eventId);
                return (false, $"Participant with User ID '{participantUserIdToUpdate}' not found for this event.", null);
            }

            // 4. Authorization
            bool isOwner = eventDocument.OwnerUserId == requestingUserId;
            bool isSelfUpdating = participantUserIdToUpdate == requestingUserId;

            if (!isOwner && !isSelfUpdating)
            {
                _logger.LogWarning("UpdateParticipantStatus: User {RequestingUserId} is not authorized to update status for participant {ParticipantUserId} in event {EventId}.",
                    requestingUserId, participantUserIdToUpdate, eventId);
                return (false, "Unauthorized. You can only update your own status or if you are the event owner.", null);
            }

            // Prevent owner from changing their own status away from "Accepted" via this method
            // (Owner status might have special meaning or be managed differently if they "decline" their own event)
            if (isSelfUpdating && participantUserIdToUpdate == eventDocument.OwnerUserId && normalizedNewStatus != "Accepted")
            {
                _logger.LogInformation("UpdateParticipantStatus: Owner {OwnerId} attempted to change their own status from 'Accepted' for event {EventId}.",
                    eventDocument.OwnerUserId, eventId);
                return (false, "The event owner's status is fixed as 'Accepted' and cannot be changed via this operation.", null);
            }


            // 5. Update status if it's different
            if (participantDocument.Status.Equals(normalizedNewStatus, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("UpdateParticipantStatus: Status for participant {ParticipantUserId} in event {EventId} is already '{CurrentStatus}'. No update needed.",
                    participantUserIdToUpdate, eventId, participantDocument.Status);
                // Return current state as success
                var user = await _userService.GetUserByIdAsync(participantDocument.UserId);
                return (true, "Status is already set to the new value.", new EventParticipantResponseDto
                {
                    EventId = participantDocument.EventId,
                    UserId = participantDocument.UserId,
                    Username = user?.Username ?? "Unknown User",
                    Status = participantDocument.Status, // Current status
                    AddedAtUtc = participantDocument.AddedAtUtc
                });
            }

            var updateDefinition = Builders<EventParticipantDocument>.Update.Set(p => p.Status, normalizedNewStatus);
            var updateResult = await _dbContext.EventParticipants.UpdateOneAsync(
                p => p.Id == participantDocument.Id, // Use the specific document ID for update
                updateDefinition);

            if (!updateResult.IsAcknowledged || updateResult.ModifiedCount == 0)
            {
                _logger.LogError("UpdateParticipantStatus: Failed to update status for participant {ParticipantUserId} in event {EventId}. DB Result: Matched={Matched}, Modified={Modified}, Ack={Ack}",
                    participantUserIdToUpdate, eventId, updateResult.MatchedCount, updateResult.ModifiedCount, updateResult.IsAcknowledged);
                return (false, "Failed to update participant status in the database.", null);
            }

            _logger.LogInformation("UpdateParticipantStatus: Status for participant {ParticipantUserId} in event {EventId} updated to '{NewStatus}' by user {RequestingUserId}.",
                participantUserIdToUpdate, eventId, normalizedNewStatus, requestingUserId);

            // 6. Return updated participant details
            var updatedUser = await _userService.GetUserByIdAsync(participantDocument.UserId); // User details shouldn't change, but good to fetch for username
            return (true, null, new EventParticipantResponseDto
            {
                EventId = participantDocument.EventId,
                UserId = participantDocument.UserId,
                Username = updatedUser?.Username ?? "Unknown User",
                Status = normalizedNewStatus, // The new status
                AddedAtUtc = participantDocument.AddedAtUtc // AddedAtUtc doesn't change
            });
        }

        public async Task<(bool Success, string? ErrorMessage)> RemoveParticipantFromEventAsync(
    string eventId, string participantUserIdToRemove, string requestingUserId)
        {
            // 1. Check if event exists
            var eventDocument = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventDocument == null)
            {
                _logger.LogWarning("RemoveParticipant: Event {EventId} not found.", eventId);
                return (false, "Event not found.");
            }

            // 2. Check if the participant record exists
            var participantDocument = await _dbContext.EventParticipants
                .Find(p => p.EventId == eventId && p.UserId == participantUserIdToRemove)
                .FirstOrDefaultAsync();

            if (participantDocument == null)
            {
                _logger.LogWarning("RemoveParticipant: Participant to remove ({ParticipantUserIdToRemove}) not found for event {EventId}.",
                    participantUserIdToRemove, eventId);
                return (false, $"Participant with User ID '{participantUserIdToRemove}' not found for this event.");
            }

            // 3. Authorization
            bool isOwnerRequesting = eventDocument.OwnerUserId == requestingUserId;
            bool isSelfRemoving = participantUserIdToRemove == requestingUserId;

            if (isOwnerRequesting && participantUserIdToRemove == eventDocument.OwnerUserId)
            {
                // Owner trying to remove themselves. This is generally not allowed directly.
                // If the owner wants to leave, they might need to delete the event or transfer ownership.
                _logger.LogWarning("RemoveParticipant: Owner {OwnerUserId} attempted to remove themselves from their own event {EventId}.",
                    requestingUserId, eventId);
                return (false, "The event owner cannot remove themselves as a participant. Consider deleting the event or transferring ownership.");
            }

            if (!isOwnerRequesting && !isSelfRemoving)
            {
                // Neither the owner nor the participant themselves is making the request.
                _logger.LogWarning("RemoveParticipant: User {RequestingUserId} is not authorized to remove participant {ParticipantUserIdToRemove} from event {EventId}.",
                    requestingUserId, participantUserIdToRemove, eventId);
                return (false, "Unauthorized. You can only remove yourself or if you are the event owner.");
            }

            // At this point, either the owner is removing someone else, or a participant is removing themselves.

            // 4. Delete the EventParticipantDocument
            var deleteResult = await _dbContext.EventParticipants.DeleteOneAsync(
                p => p.Id == participantDocument.Id); // Use the specific document ID for deletion

            if (deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0)
            {
                _logger.LogInformation("RemoveParticipant: Participant {ParticipantUserIdToRemove} successfully removed from event {EventId} by user {RequestingUserId}.",
                    participantUserIdToRemove, eventId, requestingUserId);
                return (true, null); // Success
            }
            else
            {
                _logger.LogError("RemoveParticipant: Failed to remove participant {ParticipantUserIdToRemove} from event {EventId}. DB Result: DeletedCount={DeletedCount}, Ack={Ack}",
                    participantUserIdToRemove, eventId, deleteResult.DeletedCount, deleteResult.IsAcknowledged);
                return (false, "Failed to remove participant from the event in the database.");
            }
        }
    }
}