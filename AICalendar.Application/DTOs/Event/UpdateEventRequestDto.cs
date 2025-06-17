using System;
using System.Collections.Generic; 
using System.ComponentModel.DataAnnotations;


namespace AICalendar.Application.DTOs.Event
{
    public class UpdateEventRequestDto
    {
        public string? Title { get; set; } 
        public string? Description { get; set; } 

        public DateTime? StartTimeUtc { get; set; } 
        public DateTime? EndTimeUtc { get; set; }  

        public string? TimeZoneId { get; set; } 
    }
}