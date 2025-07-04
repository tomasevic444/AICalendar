﻿using AICalendar.Application.DTOs.Event;
using AICalendar.Application.Interfaces;
using AICalendar.Domain;
using AICalendar.Infrastructure.Data;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Infrastructure.Services
{
    public class EventService : IEventService
    {
        private readonly CalendarMongoDbContext _dbContext;
        private readonly IUserService _userService; // To get usernames for participants/owner

        public EventService(CalendarMongoDbContext dbContext, IUserService userService)
        {
            _dbContext = dbContext;
            _userService = userService;
        }

        public async Task<(EventResponseDto? Event, string? ErrorMessage)> CreateEventAsync(CreateEventRequestDto createEventDto, string ownerUserId)
        {
            if (createEventDto.EndTimeUtc <= createEventDto.StartTimeUtc)
            {
                return (null, "EndTimeUtc must be after StartTimeUtc.");
            }

            var ownerUser = await _userService.GetUserByIdAsync(ownerUserId);
            if (ownerUser == null)
            {
                return (null, "Event owner not found."); // Should not happen if ownerUserId comes from valid token
            }

            var newEvent = new EventDocument
            {
                Title = createEventDto.Title,
                Description = createEventDto.Description,
                StartTimeUtc = createEventDto.StartTimeUtc,
                EndTimeUtc = createEventDto.EndTimeUtc,
                OwnerUserId = ownerUserId,
                TimeZoneId = createEventDto.TimeZoneId
                // Id will be generated by MongoDB
            };

            await _dbContext.Events.InsertOneAsync(newEvent);

            // Add the owner as a participant with "Accepted" status
            var ownerParticipant = new EventParticipantDocument
            {
                EventId = newEvent.Id,
                UserId = ownerUserId,
                Status = "Accepted", // Owner is always accepted
                AddedAtUtc = DateTime.UtcNow
            };
            await _dbContext.EventParticipants.InsertOneAsync(ownerParticipant);

            var participantDetailsList = new List<EventParticipantDetailsDto>
            {
                new EventParticipantDetailsDto { UserId = ownerUser.Id, Username = ownerUser.Username, Status = "Accepted" }
            };

            // Add other invited participants
            if (createEventDto.ParticipantUserIds != null)
            {
                foreach (var participantId in createEventDto.ParticipantUserIds.Distinct()) // Distinct to avoid duplicates
                {
                    if (participantId == ownerUserId) continue; // Owner already added

                    var participantUser = await _userService.GetUserByIdAsync(participantId);
                    if (participantUser != null)
                    {
                        var eventParticipant = new EventParticipantDocument
                        {
                            EventId = newEvent.Id,
                            UserId = participantId,
                            Status = "Invited",
                            AddedAtUtc = DateTime.UtcNow
                        };
                        await _dbContext.EventParticipants.InsertOneAsync(eventParticipant);
                        participantDetailsList.Add(new EventParticipantDetailsDto { UserId = participantUser.Id, Username = participantUser.Username, Status = "Invited" });
                    }
                    // Else: Log warning that a participant ID was not found? Or return an error?
                    // For now, we'll just skip non-existent invited users.
                }
            }

            var eventResponse = new EventResponseDto
            {
                Id = newEvent.Id,
                Title = newEvent.Title,
                Description = newEvent.Description,
                StartTimeUtc = newEvent.StartTimeUtc,
                EndTimeUtc = newEvent.EndTimeUtc,
                OwnerUserId = newEvent.OwnerUserId,
                OwnerUsername = ownerUser.Username, // Get owner's username
                TimeZoneId = newEvent.TimeZoneId,
                Participants = participantDetailsList
            };

            return (eventResponse, null);
        }



        public async Task<EventResponseDto?> GetEventByIdAsync(string eventId, string requestingUserId)
        {
            var eventDocument = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventDocument == null)
            {
                return null; // Event not found
            }

            // Fetch participants for this event
            var eventParticipants = await _dbContext.EventParticipants
                                        .Find(p => p.EventId == eventId)
                                        .ToListAsync();

            // Authorization: Check if the requesting user is the owner or one of the participants
            bool isOwner = eventDocument.OwnerUserId == requestingUserId;
            bool isParticipant = eventParticipants.Any(p => p.UserId == requestingUserId);

            if (!isOwner && !isParticipant)
            {
                return null;
            }

            // Get owner's username
            var ownerUser = await _userService.GetUserByIdAsync(eventDocument.OwnerUserId);
            string ownerUsername = ownerUser?.Username ?? "Unknown Owner";

            // Populate participant details (including their usernames)
            var participantDetailsList = new List<EventParticipantDetailsDto>();
            foreach (var participantDoc in eventParticipants)
            {
                var participantUser = await _userService.GetUserByIdAsync(participantDoc.UserId);
                participantDetailsList.Add(new EventParticipantDetailsDto
                {
                    UserId = participantDoc.UserId,
                    Username = participantUser?.Username ?? "Unknown User",
                    Status = participantDoc.Status
                });
            }

            return new EventResponseDto
            {
                Id = eventDocument.Id,
                Title = eventDocument.Title,
                Description = eventDocument.Description,
                StartTimeUtc = eventDocument.StartTimeUtc,
                EndTimeUtc = eventDocument.EndTimeUtc,
                OwnerUserId = eventDocument.OwnerUserId,
                OwnerUsername = ownerUsername,
                TimeZoneId = eventDocument.TimeZoneId,
                Participants = participantDetailsList
            };
        }

        // In AICalendar.Infrastructure.Services.EventService.cs

        public async Task<IEnumerable<EventResponseDto>> GetEventsForUserAsync(string userId, DateTime? startPeriodUtc, DateTime? endPeriodUtc)
        {
            var userParticipations = await _dbContext.EventParticipants
                                         .Find(p => p.UserId == userId)
                                         .ToListAsync();

            if (!userParticipations.Any())
            {
                return new List<EventResponseDto>();
            }

            var eventIdsUserIsInvolvedIn = userParticipations.Select(p => p.EventId).Distinct().ToList();

            // Build the filter for events
            var eventFilter = Builders<EventDocument>.Filter.In(e => e.Id, eventIdsUserIsInvolvedIn);

            if (startPeriodUtc.HasValue && endPeriodUtc.HasValue)
            {
                // Validate period if both are provided
                if (endPeriodUtc.Value <= startPeriodUtc.Value)
                {
                    return new List<EventResponseDto>(); // Return empty or handle error
                }
                // An event overlaps if: event.StartTime < period.EndTime AND event.EndTime > period.StartTime
                var periodFilter = Builders<EventDocument>.Filter.And(
                    Builders<EventDocument>.Filter.Lt(e => e.StartTimeUtc, endPeriodUtc.Value),
                    Builders<EventDocument>.Filter.Gt(e => e.EndTimeUtc, startPeriodUtc.Value)
                );
                eventFilter = Builders<EventDocument>.Filter.And(eventFilter, periodFilter);
            }


            var eventDocuments = await _dbContext.Events.Find(eventFilter).ToListAsync();

            var eventResponseList = new List<EventResponseDto>();
            // The rest of the mapping logic remains the same as in GetEventsForUserByPeriodAsync...
            foreach (var eventDoc in eventDocuments)
            {
                var allEventParticipantsDocs = await _dbContext.EventParticipants
                                                  .Find(p => p.EventId == eventDoc.Id)
                                                  .ToListAsync();

                var ownerUser = await _userService.GetUserByIdAsync(eventDoc.OwnerUserId);
                string ownerUsername = ownerUser?.Username ?? "Unknown Owner";

                var participantDetailsList = new List<EventParticipantDetailsDto>();
                foreach (var participantDoc in allEventParticipantsDocs)
                {
                    var participantUser = await _userService.GetUserByIdAsync(participantDoc.UserId);
                    participantDetailsList.Add(new EventParticipantDetailsDto
                    {
                        UserId = participantDoc.UserId,
                        Username = participantUser?.Username ?? "Unknown User",
                        Status = participantDoc.Status
                    });
                }

                eventResponseList.Add(new EventResponseDto
                {
                    Id = eventDoc.Id,
                    Title = eventDoc.Title,
                    Description = eventDoc.Description,
                    StartTimeUtc = eventDoc.StartTimeUtc,
                    EndTimeUtc = eventDoc.EndTimeUtc,
                    OwnerUserId = eventDoc.OwnerUserId,
                    OwnerUsername = ownerUsername,
                    TimeZoneId = eventDoc.TimeZoneId,
                    Participants = participantDetailsList
                });
            }

            return eventResponseList.OrderBy(e => e.StartTimeUtc);
        }

        public async Task<(bool Success, string? ErrorMessage, EventResponseDto? UpdatedEvent)> UpdateEventAsync(string eventId, UpdateEventRequestDto updateEventDto, string requestingUserId)
        {
            var existingEvent = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (existingEvent == null)
            {
                return (false, "Event not found.", null);
            }

            if (existingEvent.OwnerUserId != requestingUserId)
            {
                // TODO: Later, allow participants with specific permissions to update?
                // For now, only the owner can update the event details.
                return (false, "Unauthorized to update this event. Only the owner can modify event details.", null);
            }

            var updates = new List<UpdateDefinition<EventDocument>>();
            bool timeChanged = false;

            // Determine new start and end times
            DateTime newStartTimeUtc = updateEventDto.StartTimeUtc ?? existingEvent.StartTimeUtc;
            DateTime newEndTimeUtc = updateEventDto.EndTimeUtc ?? existingEvent.EndTimeUtc;

            // If only one time component is provided in the DTO, adjust the other to maintain duration
            // (This is Approach 2: Shift Event, Maintain Duration, if one is provided)
            if (updateEventDto.StartTimeUtc.HasValue && !updateEventDto.EndTimeUtc.HasValue)
            {
                // Start time changed, end time not provided in DTO. Maintain original duration.
                TimeSpan originalDuration = existingEvent.EndTimeUtc - existingEvent.StartTimeUtc;
                newEndTimeUtc = newStartTimeUtc + originalDuration;
                timeChanged = true;
            }
            else if (!updateEventDto.StartTimeUtc.HasValue && updateEventDto.EndTimeUtc.HasValue)
            {
                // End time changed, start time not provided in DTO. Maintain original duration by shifting start.
                TimeSpan originalDuration = existingEvent.EndTimeUtc - existingEvent.StartTimeUtc;
                newStartTimeUtc = newEndTimeUtc - originalDuration;
                timeChanged = true;
            }
            else if (updateEventDto.StartTimeUtc.HasValue && updateEventDto.EndTimeUtc.HasValue)
            {
                // Both start and end times are provided in the DTO. Use them directly.
                timeChanged = true;
            }

            // Validate the final new times
            if (timeChanged && newEndTimeUtc <= newStartTimeUtc)
            {
                return (false, "EndTimeUtc must be after StartTimeUtc.", null);
            }

            // Apply updates
            if (updateEventDto.Title != null && updateEventDto.Title != existingEvent.Title)
            {
                updates.Add(Builders<EventDocument>.Update.Set(e => e.Title, updateEventDto.Title));
            }

            // For description, null means no change. Empty string means clear the description.
            if (updateEventDto.Description != null && updateEventDto.Description != existingEvent.Description)
            {
                updates.Add(Builders<EventDocument>.Update.Set(e => e.Description, updateEventDto.Description == string.Empty ? null : updateEventDto.Description));
            }
            else if (updateEventDto.Description == string.Empty && existingEvent.Description != null) // Explicitly clearing
            {
                updates.Add(Builders<EventDocument>.Update.Set(e => e.Description, (string?)null));
            }


            if (timeChanged) // Only update times if they actually changed and are valid
            {
                if (newStartTimeUtc != existingEvent.StartTimeUtc)
                    updates.Add(Builders<EventDocument>.Update.Set(e => e.StartTimeUtc, newStartTimeUtc));
                if (newEndTimeUtc != existingEvent.EndTimeUtc)
                    updates.Add(Builders<EventDocument>.Update.Set(e => e.EndTimeUtc, newEndTimeUtc));
            }

            if (updateEventDto.TimeZoneId != null && updateEventDto.TimeZoneId != existingEvent.TimeZoneId)
            {
                updates.Add(Builders<EventDocument>.Update.Set(e => e.TimeZoneId, updateEventDto.TimeZoneId));
            }

            if (!updates.Any())
            {
                // No actual changes to apply, but we should still return the existing event data if needed.
                // Or indicate "no changes made". Let's fetch and return the current state.
                var currentEventData = await GetEventByIdAsync(eventId, requestingUserId); // Re-fetch to ensure consistency
                return (true, "No effective changes were made.", currentEventData);
            }

            var combinedUpdate = Builders<EventDocument>.Update.Combine(updates);
            var updateResult = await _dbContext.Events.UpdateOneAsync(e => e.Id == eventId, combinedUpdate);

            if (!updateResult.IsAcknowledged || updateResult.ModifiedCount == 0 && updateResult.MatchedCount > 0 && updates.Any())
            {
                return (false, "Event update failed or no effective changes were applied in the database.", null);
            }
            if (updateResult.MatchedCount == 0)
            {
                return (false, "Event not found during update (race condition or ID error).", null);
            }


            // Fetch the updated event to return it with all details, including participants
            var updatedEventDetails = await GetEventByIdAsync(eventId, requestingUserId);
            if (updatedEventDetails == null)
            {
                // This would be strange if the update succeeded
                return (false, "Failed to retrieve event details after update.", null);
            }

            return (true, null, updatedEventDetails);
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteEventAsync(string eventId, string requestingUserId)
        {
            var eventDocument = await _dbContext.Events.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventDocument == null)
            {
                return (false, "Event not found.");
            }

            if (eventDocument.OwnerUserId != requestingUserId)
            {
                return (false, "Unauthorized to delete this event. Only the owner can delete an event.");
            }

            // Perform deletion in a transaction if your MongoDB setup supports it and it's critical
            // For simplicity here, we'll do sequential deletes.

            // 1. Delete all participant records for this event
            var participantDeletionResult = await _dbContext.EventParticipants.DeleteManyAsync(p => p.EventId == eventId);
            // You can log participantDeletionResult.DeletedCount if needed

            // 2. Delete the event itself
            var eventDeletionResult = await _dbContext.Events.DeleteOneAsync(e => e.Id == eventId);

            if (eventDeletionResult.IsAcknowledged && eventDeletionResult.DeletedCount > 0)
            {
                return (true, null); // Successfully deleted
            }
            else if (eventDeletionResult.IsAcknowledged && eventDeletionResult.DeletedCount == 0)
            {
                // This means the event was found initially but gone by the time DeleteOneAsync ran (race condition?)
                // Or it was already deleted after participants were cleared.
                // If participants were deleted, we can consider it a success.
                if (participantDeletionResult.IsAcknowledged && participantDeletionResult.DeletedCount > 0)
                {
                    return (true, "Event deleted or was already gone; associated participants cleared.");
                }
                return (false, "Event deletion failed or event was not found for deletion, though it was initially fetched.");
            }

            return (false, "Event deletion was not acknowledged by the database.");
        }
    }
}