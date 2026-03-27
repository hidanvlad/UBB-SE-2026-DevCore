using DevCoreHospital.Models;
using System;
using System.Collections.Generic;

namespace DevCoreHospital.Data
{
    public interface IFatigueShiftDataSource
    {
        IReadOnlyList<RosterShift> GetShiftsForWeek(DateTime weekStart);
        IReadOnlyList<RosterShift> GetAllShifts();
        IReadOnlyList<StaffProfile> GetStaffProfiles();
        double GetMonthlyWorkedHours(int staffId, int year, int month);
        bool ReassignShift(int shiftId, int newStaffId);
    }
}

