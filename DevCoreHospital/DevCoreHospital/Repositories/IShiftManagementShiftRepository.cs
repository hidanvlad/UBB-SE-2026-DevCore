using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IShiftManagementShiftRepository
    {
        List<Shift> GetShifts();
        void AddShift(Shift newShift);
        void UpdateShiftStatus(int shiftId, ShiftStatus status);
    }
}