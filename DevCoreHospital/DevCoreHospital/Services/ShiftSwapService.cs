using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public class ShiftSwapService : IShiftSwapService
    {
        private readonly StaffRepository staffRepository;
        private readonly ShiftRepository shiftRepository;
        private readonly ShiftSwapRepository shiftSwapRepository;

        public ShiftSwapService(StaffRepository staffRepository, ShiftRepository shiftRepository, ShiftSwapRepository shiftSwapRepository)
        {
            this.staffRepository = staffRepository;
            this.shiftRepository = shiftRepository;
            this.shiftSwapRepository = shiftSwapRepository;
        }

        private static string NormalizeForComparison(string? text) => (text ?? string.Empty).Trim().ToLowerInvariant();

        public List<IStaff> GetEligibleSwapColleaguesForShift(int requesterId, int shiftId, out string error)
        {
            error = string.Empty;

            var shift = shiftRepository.GetShiftById(shiftId);
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

            var allStaff = staffRepository.LoadAllStaff();
            var requester = allStaff.FirstOrDefault(staffMember => staffMember.StaffID == requesterId);
            if (requester == null)
            {
                error = "Requester not found.";
                return new List<IStaff>();
            }

            var colleaguesWithSameProfile = new List<IStaff>();

            if (requester is Doctor requestingDoctor)
            {
                var requesterSpecialization = NormalizeForComparison(requestingDoctor.Specialization);
                colleaguesWithSameProfile = allStaff
                    .OfType<Doctor>()
                    .Where(doctor => doctor.StaffID != requesterId && NormalizeForComparison(doctor.Specialization) == requesterSpecialization)
                    .Cast<IStaff>()
                    .ToList();
            }
            else if (requester is Pharmacyst requestingPharmacist)
            {
                var requesterCertification = NormalizeForComparison(requestingPharmacist.Certification);
                colleaguesWithSameProfile = allStaff
                    .OfType<Pharmacyst>()
                    .Where(pharmacist => pharmacist.StaffID != requesterId && NormalizeForComparison(pharmacist.Certification) == requesterCertification)
                    .Cast<IStaff>()
                    .ToList();
            }

            return colleaguesWithSameProfile
                .Where(colleague => !shiftRepository.IsStaffWorkingDuring(colleague.StaffID, shift.StartTime, shift.EndTime))
                .ToList();
        }

        public bool RequestShiftSwap(int requesterId, int shiftId, int colleagueId, out string message)
        {
            message = string.Empty;

            var eligibleColleagues = GetEligibleSwapColleaguesForShift(requesterId, shiftId, out var validationError);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                message = validationError;
                return false;
            }

            if (!eligibleColleagues.Any(colleague => colleague.StaffID == colleagueId))
            {
                message = "Selected colleague is not eligible (must be same profile and free in interval).";
                return false;
            }

            var shift = shiftRepository.GetShiftById(shiftId);
            var requester = staffRepository.GetStaffById(requesterId);
            if (requester == null)
            {
                message = "Requester not found.";
                return false;
            }

            var swapRequest = new ShiftSwapRequest
            {
                ShiftId = shiftId,
                RequesterId = requesterId,
                ColleagueId = colleagueId,
                RequestedAt = DateTime.UtcNow,
                Status = ShiftSwapRequestStatus.PENDING,
            };

            var swapId = shiftSwapRepository.CreateShiftSwapRequest(swapRequest);
            if (swapId <= 0)
            {
                message = "Failed to create shift swap request.";
                return false;
            }

            shiftSwapRepository.AddNotification(
                colleagueId,
                "New Shift Swap Request",
                $"You received a shift swap request from {requester.FirstName} {requester.LastName} for shift #{shiftId} ({shift.StartTime:yyyy-MM-dd HH:mm} - {shift.EndTime:HH:mm}).");

            message = "Shift swap request sent successfully.";
            return true;
        }

        public List<ShiftSwapRequest> GetIncomingSwapRequests(int colleagueId)
        {
            return shiftSwapRepository.GetPendingSwapRequestsForColleague(colleagueId);
        }

        public bool AcceptSwapRequest(int swapId, int colleagueId, out string message)
        {
            message = string.Empty;

            var swapRequest = shiftSwapRepository.GetShiftSwapRequestById(swapId);
            if (swapRequest == null)
            {
                message = "Swap request not found.";
                return false;
            }

            if (swapRequest.ColleagueId != colleagueId)
            {
                message = "You cannot accept this request.";
                return false;
            }

            if (swapRequest.Status != ShiftSwapRequestStatus.PENDING)
            {
                message = "This request is no longer pending.";
                return false;
            }

            var shift = shiftRepository.GetShiftById(swapRequest.ShiftId);
            if (shift == null)
            {
                message = "Shift not found.";
                return false;
            }

            if (shiftRepository.IsStaffWorkingDuring(colleagueId, shift.StartTime, shift.EndTime))
            {
                message = "You are already scheduled to work in that interval.";
                return false;
            }

            if (!shiftSwapRepository.ReassignShiftToStaff(swapRequest.ShiftId, colleagueId))
            {
                message = "Failed to reassign shift.";
                return false;
            }

            shiftSwapRepository.UpdateShiftSwapRequestStatus(swapId, "ACCEPTED");
            shiftSwapRepository.AddNotification(swapRequest.RequesterId, "Shift Swap Accepted", $"Your swap request #{swapId} was accepted.");
            shiftRepository.Refresh();

            message = "Swap accepted.";
            return true;
        }

        public bool RejectSwapRequest(int swapId, int colleagueId, out string message)
        {
            message = string.Empty;

            var swapRequest = shiftSwapRepository.GetShiftSwapRequestById(swapId);
            if (swapRequest == null)
            {
                message = "Swap request not found.";
                return false;
            }

            if (swapRequest.ColleagueId != colleagueId)
            {
                message = "You cannot reject this request.";
                return false;
            }

            if (swapRequest.Status != ShiftSwapRequestStatus.PENDING)
            {
                message = "This request is no longer pending.";
                return false;
            }

            shiftSwapRepository.UpdateShiftSwapRequestStatus(swapId, "REJECTED");
            shiftSwapRepository.AddNotification(swapRequest.RequesterId, "Shift Swap Rejected", $"Your swap request #{swapId} was rejected.");
            message = "Swap rejected.";
            return true;
        }
    }
}
