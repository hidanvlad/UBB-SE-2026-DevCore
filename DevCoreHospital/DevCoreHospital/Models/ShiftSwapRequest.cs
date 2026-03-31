using System;

namespace DevCoreHospital.Models
{
    public enum ShiftSwapRequestStatus
    {
        PENDING,
        ACCEPTED,
        REJECTED,
        CANCELLED
    }

    public class ShiftSwapRequest
    {
        public int SwapId { get; set; }
        public int ShiftId { get; set; }
        public int RequesterId { get; set; }
        public int ColleagueId { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public ShiftSwapRequestStatus Status { get; set; } = ShiftSwapRequestStatus.PENDING;

        public ShiftSwapRequest() { }

        public ShiftSwapRequest(int swapId, int shiftId, int requesterId, int colleagueId)
        {
            SwapId = swapId;
            ShiftId = shiftId;
            RequesterId = requesterId;
            ColleagueId = colleagueId;
            RequestedAt = DateTime.UtcNow;
            Status = ShiftSwapRequestStatus.PENDING;
        }
    }
}