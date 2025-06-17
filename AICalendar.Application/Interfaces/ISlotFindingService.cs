using AICalendar.Application.DTOs.Availability;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICalendar.Application.Interfaces
{
    public interface ISlotFindingService
    {
        Task<IEnumerable<AvailableSlotDto>> FindAvailableSlotsAsync(FindSlotsRequestDto request);
    }
}
