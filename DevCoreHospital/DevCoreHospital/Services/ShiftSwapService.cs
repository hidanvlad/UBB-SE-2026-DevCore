using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public class ShiftSwapService : IShiftSwapService
    {
        private const string AcceptedStatus = "ACCEPTED";
        private const string RejectedStatus = "REJECTED";
        private const string SwapRequestNotificationTitle = "New Shift Swap Request";
        private const string SwapAcceptedNotificationTitle = "Shift Swap Accepted";
        private const string SwapRejectedNotificationTitle = "Shift Swap Rejected";

        private readonly IStaffRepository staffRepository;
        private readonly IShiftRepository shiftRepository;
        private readonly IShiftSwapRepository shiftSwapRepository;
        private readonly INotificationRepository notificationRepository;

        public ShiftSwapService(
            IStaffRepository staffRepository,
            IShiftRepository shiftRepository,
            IShiftSwapRepository shiftSwapRepository,
            INotificationRepository notificationRepository)
        {
            this.staffRepository = staffRepository;
            this.shiftRepository = shiftRepository;
            this.shiftSwapRepository = shiftSwapRepository;
            this.notificationRepository = notificationRepository;
        }

        public List<Shift> GetFutureShiftsForStaff(int staffId)
        {
            bool IsFutureShiftForStaff(Shift shift) =>
                shift.AppointedStaff.StaffID == staffId && shift.StartTime > DateTime.Now;
            DateTime SortKey(Shift shift) => shift.StartTime;
            return shiftRepository.GetAllShifts()
                .Where(IsFutureShiftForStaff)
                .OrderBy(SortKey)
                .ToList();
        }

        private static string NormalizeForComparison(string? text) => (text ?? string.Empty).Trim().ToLowerInvariant();

        public List<IStaff> GetEligibleSwapColleaguesForShift(int requesterId, int shiftId, out string error)
        {
            error = string.Empty;

            var allShifts = shiftRepository.GetAllShifts();
            bool HasMatchingShiftId(Shift existingShift) => existingShift.Id == shiftId;
            var shift = allShifts.FirstOrDefault(HasMatchingShiftId);
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
            bool HasRequesterId(IStaff staffMember) => staffMember.StaffID == requesterId;
            var requester = allStaff.FirstOrDefault(HasRequesterId);
            if (requester == null)
            {
                error = "Requester not found.";
                return new List<IStaff>();
            }

            var colleaguesWithSameProfile = new List<IStaff>();

            bool IsScheduledOrActive(Shift scheduledShift) =>
                scheduledShift.Status == ShiftStatus.SCHEDULED || scheduledShift.Status == ShiftStatus.ACTIVE;

            if (requester is Doctor requestingDoctor)
            {
                var requesterSpecialization = NormalizeForComparison(requestingDoctor.Specialization);
                bool HasMatchingSpecialization(Doctor doctor) =>
                    doctor.StaffID != requesterId && NormalizeForComparison(doctor.Specialization) == requesterSpecialization;

                colleaguesWithSameProfile = allStaff
                    .OfType<Doctor>()
                    .Where(HasMatchingSpecialization)
                    .Cast<IStaff>()
                    .ToList();
            }
            else if (requester is Pharmacyst requestingPharmacist)
            {
                var requesterCertification = NormalizeForComparison(requestingPharmacist.Certification);
                bool HasMatchingCertification(Pharmacyst pharmacist) =>
                    pharmacist.StaffID != requesterId && NormalizeForComparison(pharmacist.Certification) == requesterCertification;

                colleaguesWithSameProfile = allStaff
                    .OfType<Pharmacyst>()
                    .Where(HasMatchingCertification)
                    .Cast<IStaff>()
                    .ToList();
            }

            bool HasNoOverlappingShifts(IStaff colleague)
            {
                bool OverlapsTargetShift(Shift existingShift) =>
                    existingShift.AppointedStaff.StaffID == colleague.StaffID
                    && existingShift.StartTime < shift.EndTime
                    && existingShift.EndTime > shift.StartTime
                    && IsScheduledOrActive(existingShift);
                return !allShifts.Any(OverlapsTargetShift);
            }

            return colleaguesWithSameProfile
                .Where(HasNoOverlappingShifts)
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

            bool HasMatchingColleagueId(IStaff colleague) => colleague.StaffID == colleagueId;
            if (!eligibleColleagues.Any(HasMatchingColleagueId))
            {
                message = "Selected colleague is not eligible (must be same profile and free in interval).";
                return false;
            }

            bool HasMatchingShiftIdInLookup(Shift existingShift) => existingShift.Id == shiftId;
            var shift = shiftRepository.GetAllShifts().FirstOrDefault(HasMatchingShiftIdInLookup);
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

            var swapId = shiftSwapRepository.AddShiftSwapRequest(swapRequest);
            if (swapId <= 0)
            {
                message = "Failed to create shift swap request.";
                return false;
            }

            notificationRepository.AddNotification(
                colleagueId,
                SwapRequestNotificationTitle,
                $"You received a shift swap request from {requester.FirstName} {requester.LastName} for shift #{shiftId} ({shift!.StartTime:yyyy-MM-dd HH:mm} - {shift.EndTime:HH:mm}).");

            message = "Shift swap request sent successfully.";
            return true;
        }

        public List<ShiftSwapRequest> GetIncomingSwapRequests(int colleagueId)
        {
            bool IsPendingForColleague(ShiftSwapRequest swapRequest) =>
                swapRequest.ColleagueId == colleagueId && swapRequest.Status == ShiftSwapRequestStatus.PENDING;
            DateTime ByRequestedAt(ShiftSwapRequest swapRequest) => swapRequest.RequestedAt;

            return shiftSwapRepository.GetAllShiftSwapRequests()
                .Where(IsPendingForColleague)
                .OrderByDescending(ByRequestedAt)
                .ToList();
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

            var allShifts = shiftRepository.GetAllShifts();
            bool HasTargetShiftId(Shift existingShift) => existingShift.Id == swapRequest.ShiftId;
            var shift = allShifts.FirstOrDefault(HasTargetShiftId);
            if (shift == null)
            {
                message = "Shift not found.";
                return false;
            }

            bool IsScheduledOrActive(Shift scheduledShift) =>
                scheduledShift.Status == ShiftStatus.SCHEDULED || scheduledShift.Status == ShiftStatus.ACTIVE;

            bool ColleagueOverlapsTargetShift(Shift existingShift) =>
                existingShift.AppointedStaff.StaffID == colleagueId
                && existingShift.StartTime < shift.EndTime
                && existingShift.EndTime > shift.StartTime
                && IsScheduledOrActive(existingShift);

            if (allShifts.Any(ColleagueOverlapsTargetShift))
            {
                message = "You are already scheduled to work in that interval.";
                return false;
            }

            shiftRepository.UpdateShiftStaffId(swapRequest.ShiftId, colleagueId);
            shiftSwapRepository.UpdateShiftSwapRequestStatus(swapId, AcceptedStatus);
            notificationRepository.AddNotification(
                swapRequest.RequesterId,
                SwapAcceptedNotificationTitle,
                $"Your swap request #{swapId} was accepted.");

            message = "Swap accepted.";
            return true;
        }

        public List<Doctor> GetAllDoctors()
        {
            string ByFirstName(Doctor doctor) => doctor.FirstName;
            string ByLastName(Doctor doctor) => doctor.LastName;

            return staffRepository.LoadAllStaff()
                .OfType<Doctor>()
                .OrderBy(ByFirstName)
                .ThenBy(ByLastName)
                .ToList();
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

            shiftSwapRepository.UpdateShiftSwapRequestStatus(swapId, RejectedStatus);
            notificationRepository.AddNotification(
                swapRequest.RequesterId,
                SwapRejectedNotificationTitle,
                $"Your swap request #{swapId} was rejected.");
            message = "Swap rejected.";
            return true;
        }
    }
}
