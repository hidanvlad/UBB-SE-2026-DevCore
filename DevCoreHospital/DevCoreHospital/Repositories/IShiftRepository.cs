using System;
using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories;

public interface IShiftRepository
{
    float GetWeeklyHours(int staffId);

    IReadOnlyList<Shift> GetShiftsForStaffInRange(int staffId, DateTime rangeStart, DateTime rangeEnd);

    Shift? GetShiftById(int shiftId);

    List<Shift> GetShiftsByStaffID(int staffId);

    bool IsStaffWorkingDuring(int staffId, DateTime startTime, DateTime endTime);

    void Refresh();
}
