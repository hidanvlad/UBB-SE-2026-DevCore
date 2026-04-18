using System;
using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IStaffAndShiftService
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
        List<IStaff> GetEligibleSwapColleaguesForShift(int requesterId, int shiftId, out string error);
        bool RequestShiftSwap(int requesterId, int shiftId, int colleagueId, out string message);
        List<ShiftSwapRequest> GetIncomingSwapRequests(int colleagueId);
        bool AcceptSwapRequest(int swapId, int colleagueId, out string message);
        bool RejectSwapRequest(int swapId, int colleagueId, out string message);
        List<string> GetSpecializationsAndCertificationsForLocation(string location);
    }
}
