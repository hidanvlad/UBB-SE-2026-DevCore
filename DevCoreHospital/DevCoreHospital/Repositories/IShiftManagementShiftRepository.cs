using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IShiftManagementShiftRepository
    {
        IReadOnlyList<Shift> GetAllShifts();
        void AddShift(Shift newShift);
        void UpdateShiftStatus(int shiftId, ShiftStatus status);
    }
}
