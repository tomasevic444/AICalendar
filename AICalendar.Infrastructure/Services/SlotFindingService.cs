using AICalendar.Application.DTOs.Availability;
using AICalendar.Application.Interfaces;
using AICalendar.Domain;
using AICalendar.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Infrastructure.Services
{
    public class SlotFindingService : ISlotFindingService
    {
        private readonly CalendarMongoDbContext _dbContext;
        private readonly ILogger<SlotFindingService> _logger;

        public SlotFindingService(CalendarMongoDbContext dbContext, ILogger<SlotFindingService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<IEnumerable<AvailableSlotDto>> FindAvailableSlotsAsync(FindSlotsRequestDto request)
        {
            if (request.SearchWindowEndUtc <= request.SearchWindowStartUtc)
            {
                _logger.LogWarning("FindAvailableSlots: Search window end is not after start.");
                return Enumerable.Empty<AvailableSlotDto>();
            }
            if (request.MeetingDurationMinutes <= 0)
            {
                _logger.LogWarning("FindAvailableSlots: Meeting duration must be positive.");
                return Enumerable.Empty<AvailableSlotDto>();
            }

            var meetingDuration = TimeSpan.FromMinutes(request.MeetingDurationMinutes);
            var allBusySlotsForParticipants = new List<TimeRange>();

            // 1. Fetch all events for all participants that overlap the search window
            // An event overlaps if: event.StartTime < window.EndTime AND event.EndTime > window.StartTime
            var relevantEventsFilter = Builders<EventDocument>.Filter.And(
                Builders<EventDocument>.Filter.Lt(e => e.StartTimeUtc, request.SearchWindowEndUtc),
                Builders<EventDocument>.Filter.Gt(e => e.EndTimeUtc, request.SearchWindowStartUtc)
            );

            // Find events where any of the requested participants are involved
            // This can be done by first getting participations, then events OR a more complex event query
            // Let's get participations first for clarity, then filter events

            var participantEventIds = new HashSet<string>();
            foreach (var userId in request.ParticipantUserIds.Distinct())
            {
                var participations = await _dbContext.EventParticipants
                    .Find(p => p.UserId == userId && (p.Status == "Accepted" || p.Status == "Tentative")) // Consider only confirmed/tentative
                    .Project(p => p.EventId) // Project only EventId
                    .ToListAsync();
                foreach (var eventId in participations)
                {
                    participantEventIds.Add(eventId);
                }
            }

            if (!participantEventIds.Any())
            {
                _logger.LogInformation("FindAvailableSlots: No relevant event participations found for the given users.");
                // If no events, the entire window is free (subject to working hours if implemented)
                // Fall through to FindFreeSlotsInWindow with an empty mergedBusySlots list
            }


            if (participantEventIds.Any())
            {
                var eventIdFilter = Builders<EventDocument>.Filter.In(e => e.Id, participantEventIds);
                var finalEventFilter = Builders<EventDocument>.Filter.And(relevantEventsFilter, eventIdFilter);

                var events = await _dbContext.Events.Find(finalEventFilter).ToListAsync();

                foreach (var ev in events)
                {
                    allBusySlotsForParticipants.Add(new TimeRange(ev.StartTimeUtc, ev.EndTimeUtc));
                }
            }


            // 2. Sort and Merge all busy slots
            // The AvailabilityAlgorithm.MergeBusySlots expects sorted input if not sorting internally
            allBusySlotsForParticipants.Sort((x, y) => x.StartTimeUtc.CompareTo(y.StartTimeUtc));
            var mergedBusySlots = AvailabilityAlgorithm.MergeBusySlots(allBusySlotsForParticipants);
            _logger.LogInformation("FindAvailableSlots: Merged {Count} busy slots for participants.", mergedBusySlots.Count);


            // TODO: Optionally, add "working hours" as busy slots if they are outside the working window
            // For each day in the search window, add busy slots from 00:00 to WorkingHoursStart
            // and from WorkingHoursEnd to 23:59:59. This requires more complex iteration.
            // For now, we assume 24/7 availability within the search window.


            // 3. Find free slots
            var freeTimeRanges = AvailabilityAlgorithm.FindFreeSlotsInWindow(
                request.SearchWindowStartUtc,
                request.SearchWindowEndUtc,
                mergedBusySlots,
                meetingDuration // Use the requested meeting duration as minDuration for a slot
            );
            _logger.LogInformation("FindAvailableSlots: Found {Count} potential free time ranges.", freeTimeRanges.Count);


            // 4. Map to DTO
            return freeTimeRanges.Select(tr => new AvailableSlotDto
            {
                StartTimeUtc = tr.StartTimeUtc,
                EndTimeUtc = tr.EndTimeUtc
            });
        }
    }
    public static class AvailabilityAlgorithm
    {
        // Merges overlapping or adjacent busy time ranges.
        // Input busySlots must be sorted by StartTimeUtc.
        public static List<TimeRange> MergeBusySlots(List<TimeRange> busySlots)
        {
            if (busySlots == null || !busySlots.Any())
            {
                return new List<TimeRange>();
            }

            // Ensure sorted for merging (caller should ideally sort, but defensive sort here)
            // busySlots.Sort(); // If TimeRange implements IComparable correctly

            var mergedSlots = new List<TimeRange>();
            var currentMerge = busySlots[0];

            for (int i = 1; i < busySlots.Count; i++)
            {
                var nextSlot = busySlots[i];
                // If nextSlot overlaps or is adjacent to currentMerge
                if (nextSlot.StartTimeUtc <= currentMerge.EndTimeUtc)
                {
                    // Merge them by extending the EndTimeUtc of currentMerge if nextSlot ends later
                    if (nextSlot.EndTimeUtc > currentMerge.EndTimeUtc)
                    {
                        currentMerge = new TimeRange(currentMerge.StartTimeUtc, nextSlot.EndTimeUtc);
                    }
                }
                else
                {
                    // No overlap, currentMerge is complete
                    mergedSlots.Add(currentMerge);
                    currentMerge = nextSlot; // Start a new merge with nextSlot
                }
            }
            mergedSlots.Add(currentMerge); // Add the last merged slot

            return mergedSlots;
        }

        // Finds free time slots within a search window, given a list of merged busy slots.
        public static List<TimeRange> FindFreeSlotsInWindow(
            DateTime windowStartUtc,
            DateTime windowEndUtc,
            List<TimeRange> mergedBusySlots,
            TimeSpan minDuration)
        {
            var freeSlots = new List<TimeRange>();
            var currentTime = windowStartUtc;

            foreach (var busySlot in mergedBusySlots)
            {
                // If there's a gap between currentTime and the start of the busySlot
                if (busySlot.StartTimeUtc > currentTime)
                {
                    var potentialFreeSlotEnd = Min(busySlot.StartTimeUtc, windowEndUtc);
                    var freeDuration = potentialFreeSlotEnd - currentTime;
                    if (freeDuration >= minDuration)
                    {
                        freeSlots.Add(new TimeRange(currentTime, potentialFreeSlotEnd));
                    }
                }
                // Move currentTime past the current busySlot, ensuring it doesn't go beyond windowEnd
                currentTime = Max(currentTime, Min(busySlot.EndTimeUtc, windowEndUtc));

                if (currentTime >= windowEndUtc) break; // No more time left in window
            }

            // Check for any remaining free time after the last busy slot until the windowEnd
            if (currentTime < windowEndUtc)
            {
                var freeDuration = windowEndUtc - currentTime;
                if (freeDuration >= minDuration)
                {
                    freeSlots.Add(new TimeRange(currentTime, windowEndUtc));
                }
            }

            return freeSlots;
        }

        private static DateTime Min(DateTime d1, DateTime d2) => d1 < d2 ? d1 : d2;
        private static DateTime Max(DateTime d1, DateTime d2) => d1 > d2 ? d1 : d2;
    }
}

