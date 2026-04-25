using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories;

public interface IShiftRepository
{
    IReadOnlyList<Shift> GetAllShifts();

    void AddShift(Shift newShift);

    void UpdateShiftStatus(int shiftId, ShiftStatus status);

    void UpdateShiftStaffId(int shiftId, int newStaffId);

    void DeleteShift(int shiftId);
}
