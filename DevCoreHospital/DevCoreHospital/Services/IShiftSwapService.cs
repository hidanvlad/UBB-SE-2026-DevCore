using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IShiftSwapService
    {
        List<IStaff> GetEligibleSwapColleaguesForShift(int requesterId, int shiftId, out string error);
        bool RequestShiftSwap(int requesterId, int shiftId, int colleagueId, out string message);
        List<ShiftSwapRequest> GetIncomingSwapRequests(int colleagueId);
        bool AcceptSwapRequest(int swapId, int colleagueId, out string message);
        bool RejectSwapRequest(int swapId, int colleagueId, out string message);
    }
}
