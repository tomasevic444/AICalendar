using System;
using System.Collections.Generic;

namespace AICalendar.Application.DTOs.Event
{
    public class EventResponseDto
    {
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty; 
    public string? TimeZoneId { get; set; } 
    public List<EventParticipantDetailsDto> Participants { get; set; } = new List<EventParticipantDetailsDto>();
}
}
