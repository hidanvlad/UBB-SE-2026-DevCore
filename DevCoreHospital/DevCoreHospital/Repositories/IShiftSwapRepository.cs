using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IShiftSwapRepository
    {
        int AddShiftSwapRequest(ShiftSwapRequest request);
        IReadOnlyList<ShiftSwapRequest> GetAllShiftSwapRequests();
        ShiftSwapRequest? GetShiftSwapRequestById(int swapId);
        void UpdateShiftSwapRequestStatus(int swapId, string status);
    }
}
