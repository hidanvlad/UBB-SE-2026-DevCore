using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IShiftSwapRepository
    {
        int CreateShiftSwapRequest(ShiftSwapRequest request);

        List<ShiftSwapRequest> GetPendingSwapRequestsForColleague(int colleagueId);

        ShiftSwapRequest? GetShiftSwapRequestById(int swapId);

        bool UpdateShiftSwapRequestStatus(int swapId, string status);

        void AddNotification(int recipientStaffId, string title, string message);

        bool ReassignShiftToStaff(int shiftId, int newStaffId);
    }
}
