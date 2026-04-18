using System;
using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IShiftManagementService
    {
        void SetShiftActive(int shiftId);
        void CancelShift(int shiftId);
        bool ValidateNoOverlap(int staffId, DateTime start, DateTime end);
        void AddShift(Shift shift);
        List<Shift> GetDailyShifts(DateTime date);
        List<Shift> GetWeeklyShifts(DateTime date);
        bool ReassignShift(Shift shift, IStaff newStaff);
        List<IStaff> GetFilteredStaff(string location, string requiredSpecializationOrCertification);
        List<IStaff> FindStaffReplacements(Shift shift);
        List<string> GetSpecializationsAndCertificationsForLocation(string location);
    }
}
