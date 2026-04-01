using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Data;
using Microsoft.IdentityModel.Tokens;

namespace DevCoreHospital.Services
{
    public class StaffAndShiftService
    {
        private readonly StaffRepository _staffRepo;
        private readonly ShiftRepository _shiftRepo;
        private readonly DatabaseManager _dbManager;

        public StaffAndShiftService(StaffRepository staffRepo, ShiftRepository shiftRepo, DatabaseManager dbManager)
        {
            _staffRepo = staffRepo;
            _shiftRepo = shiftRepo;
            _dbManager = dbManager;
        }

        public void SetShiftActive(int shiftId)
        {
            var shift = _shiftRepo.GetShifts().FirstOrDefault(s => s.Id == shiftId);
            if (shift != null)
            {
                _shiftRepo.UpdateShiftStatus(shiftId, ShiftStatus.ACTIVE);
                _staffRepo.UpdateStaffAvailability(shift.AppointedStaff.StaffID, true, DoctorStatus.AVAILABLE);
            }
        }

        public void CancelShift(int shiftId)
        {
            var shift = _shiftRepo.GetShifts().FirstOrDefault(s => s.Id == shiftId);
            if (shift != null)
            {
                _staffRepo.UpdateStaffAvailability(shift.AppointedStaff.StaffID, false, DoctorStatus.OFF_DUTY);
                _shiftRepo.UpdateShiftStatus(shiftId, ShiftStatus.COMPLETED);
            }
        }

        public bool ValidateNoOverlap(int staffId, DateTime start, DateTime end)
        {
            return !_shiftRepo.GetShifts().Any(shift => (shift.AppointedStaff.StaffID == staffId) &&
                ((start >= shift.StartTime && start < shift.EndTime) || (end > shift.StartTime && end <= shift.EndTime)));
        }

        public void AddShift(Shift shift) => _shiftRepo.AddShift(shift);

        public List<Shift> GetDailyShifts(DateTime date)
            => _shiftRepo.GetShifts().Where(shift => shift.StartTime.Date == date.Date).ToList();

        public List<Shift> GetWeeklyShifts(DateTime date)
        {
            var monday = date.AddDays(-(int)DateTime.Now.DayOfWeek + (int)DayOfWeek.Monday);
            var sunday = monday.AddDays(7);
            return _shiftRepo.GetShifts().Where(shift => shift.StartTime >= monday && shift.StartTime < sunday).ToList();
        }

        public bool ReassignShift(Shift shift, IStaff newStaff)
        {
            if (shift == null || newStaff == null) return false;
            shift.AppointedStaff = newStaff;
            return true;
        }

        // ========================= VIEW MODEL METHODS =========================

        public List<IStaff> GetFilteredStaff(string location, string requiredSpecializationOrCertification)
        {
            var allStaff = _staffRepo.LoadAllStaff();
            var filteredStaff = new List<IStaff>();

            if (location.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase))
            {
                filteredStaff.AddRange(allStaff.OfType<Pharmacyst>()
                    .Where(p => p.Certification.Contains(requiredSpecializationOrCertification, StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                filteredStaff.AddRange(allStaff.OfType<Doctor>()
                    .Where(d => d.Specialization.Contains(requiredSpecializationOrCertification, StringComparison.OrdinalIgnoreCase)));
            }

            return filteredStaff;
        }

        public List<IStaff> FindStaffReplacements(Shift shift)
        {
            if (shift == null || shift.AppointedStaff == null) return new List<IStaff>();

            var currentStaff = shift.AppointedStaff;
            var allStaff = _staffRepo.LoadAllStaff();

            return allStaff.Where(s =>
                s.GetType() == currentStaff.GetType() &&
                s.StaffID != currentStaff.StaffID &&
                ValidateNoOverlap(s.StaffID, shift.StartTime, shift.EndTime)
            ).ToList();
        }

        private static string N(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        // ========================= SHIFT SWAP - REQUEST =========================
        public List<IStaff> GetEligibleSwapColleaguesForShift(int requesterId, int shiftId, out string error)
        {
            error = string.Empty;

            var shift = _shiftRepo.GetShiftById(shiftId);
            if (shift == null)
            {
                error = "Shift not found.";
                return new List<IStaff>();
            }

            if (shift.AppointedStaff.StaffID != requesterId)
            {
                error = "You can only request swap for your own shift.";
                return new List<IStaff>();
            }

            if (shift.StartTime <= DateTime.Now)
            {
                error = "You can only request swap for a future shift.";
                return new List<IStaff>();
            }

            // Use fresh full staff list
            var all = _staffRepo.LoadAllStaff();
            var requester = all.FirstOrDefault(s => s.StaffID == requesterId);
            if (requester == null)
            {
                error = "Requester not found.";
                return new List<IStaff>();
            }

            List<IStaff> sameProfile = new();

            if (requester is Doctor reqDoc)
            {
                var reqSpec = N(reqDoc.Specialization);
                sameProfile = all
                    .OfType<Doctor>()
                    .Where(d => d.StaffID != requesterId && N(d.Specialization) == reqSpec)
                    .Cast<IStaff>()
                    .ToList();
            }
            else if (requester is Pharmacyst reqPh)
            {
                var reqCert = N(reqPh.Certification);
                sameProfile = all
                    .OfType<Pharmacyst>()
                    .Where(p => p.StaffID != requesterId && N(p.Certification) == reqCert)
                    .Cast<IStaff>()
                    .ToList();
            }

            // Must be FREE during requester shift
            var freeColleagues = sameProfile
                .Where(c => !_shiftRepo.IsStaffWorkingDuring(c.StaffID, shift.StartTime, shift.EndTime))
                .ToList();

            return freeColleagues;
        }

        public bool RequestShiftSwap(int requesterId, int shiftId, int colleagueId, out string message)
        {
            message = string.Empty;

            var eligible = GetEligibleSwapColleaguesForShift(requesterId, shiftId, out var err);
            if (!string.IsNullOrWhiteSpace(err))
            {
                message = err;
                return false;
            }

            if (!eligible.Any(c => c.StaffID == colleagueId))
            {
                message = "Selected colleague is not eligible (must be same profile and free in interval).";
                return false;
            }

            var shift = _shiftRepo.GetShiftById(shiftId)!;
            var requester = _staffRepo.GetStaffById(requesterId);
            if (requester == null)
            {
                message = "Requester not found.";
                return false;
            }

            var request = new ShiftSwapRequest
            {
                ShiftId = shiftId,
                RequesterId = requesterId,
                ColleagueId = colleagueId,
                RequestedAt = DateTime.UtcNow,
                Status = ShiftSwapRequestStatus.PENDING
            };

            var swapId = _dbManager.CreateShiftSwapRequest(request);
            if (swapId <= 0)
            {
                message = "Failed to create shift swap request.";
                return false;
            }

            _dbManager.AddNotification(
                colleagueId,
                "New Shift Swap Request",
                $"You received a shift swap request from {requester.FirstName} {requester.LastName} for shift #{shiftId} ({shift.StartTime:yyyy-MM-dd HH:mm} - {shift.EndTime:HH:mm}).");

            message = "Shift swap request sent successfully.";
            return true;
        }

        public List<ShiftSwapRequest> GetIncomingSwapRequests(int colleagueId)
        {
            return _dbManager.GetPendingSwapRequestsForColleague(colleagueId);
        }

        public bool AcceptSwapRequest(int swapId, int colleagueId, out string message)
        {
            message = string.Empty;

            var req = _dbManager.GetShiftSwapRequestById(swapId);
            if (req == null) { message = "Swap request not found."; return false; }
            if (req.ColleagueId != colleagueId) { message = "You cannot accept this request."; return false; }
            if (req.Status != ShiftSwapRequestStatus.PENDING) { message = "This request is no longer pending."; return false; }

            var shift = _shiftRepo.GetShiftById(req.ShiftId);
            if (shift == null) { message = "Shift not found."; return false; }

            // must still be free
            if (_shiftRepo.IsStaffWorkingDuring(colleagueId, shift.StartTime, shift.EndTime))
            {
                message = "You are already scheduled to work in that interval.";
                return false;
            }

            if (!_dbManager.ReassignShiftToStaff(req.ShiftId, colleagueId))
            {
                message = "Failed to reassign shift.";
                return false;
            }

            _dbManager.UpdateShiftSwapRequestStatus(swapId, "ACCEPTED");
            _dbManager.AddNotification(req.RequesterId, "Shift Swap Accepted", $"Your swap request #{swapId} was accepted.");
            _shiftRepo.Refresh();

            message = "Swap accepted.";
            return true;
        }

        public bool RejectSwapRequest(int swapId, int colleagueId, out string message)
        {
            message = string.Empty;

            var req = _dbManager.GetShiftSwapRequestById(swapId);
            if (req == null) { message = "Swap request not found."; return false; }
            if (req.ColleagueId != colleagueId) { message = "You cannot reject this request."; return false; }
            if (req.Status != ShiftSwapRequestStatus.PENDING) { message = "This request is no longer pending."; return false; }

            _dbManager.UpdateShiftSwapRequestStatus(swapId, "REJECTED");
            _dbManager.AddNotification(req.RequesterId, "Shift Swap Rejected", $"Your swap request #{swapId} was rejected.");
            message = "Swap rejected.";
            return true;
        }

        public List<string> GetSpecializationsAndCertificationsForLocation(string location)
        {
            List<string> result = new List<string>();
            var allStaff = _staffRepo.LoadAllStaff();

            if (location.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(allStaff.OfType<Pharmacyst>()
                    .Where(p => !string.IsNullOrEmpty(p.Certification))
                    .Select(p => p.Certification));
            }
            else
            {
                result.AddRange(allStaff.OfType<Doctor>()
                    .Where(d => !string.IsNullOrEmpty(d.Specialization))
                    .Select(d => d.Specialization));
            }

            result = result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            return result;
        }
    }
}