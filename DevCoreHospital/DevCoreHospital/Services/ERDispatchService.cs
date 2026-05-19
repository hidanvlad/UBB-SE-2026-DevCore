using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public sealed class ERDispatchService : IERDispatchService
    {
        private const string PendingStatus = "PENDING";
        private const string AssignedStatus = "ASSIGNED";
        private const string UnmatchedStatus = "UNMATCHED";
        private const string DefaultSpecialization = "General";
        private const string FallbackLocation = "Ward A";

        private static readonly (string Specialization, string Location)[] FallbackTemplates =
        {
            ("Surgeon", FallbackLocation),
            ("Cardiology", FallbackLocation),
            ("Neurology", FallbackLocation),
            ("Pediatrics", FallbackLocation),
        };

        private readonly IERDispatchRepository requestRepository;
        private readonly IStaffRepository staffRepository;
        private readonly IShiftRepository shiftRepository;

        public ERDispatchService(
            IERDispatchRepository requestRepository,
            IStaffRepository staffRepository,
            IShiftRepository shiftRepository)
        {
            this.requestRepository = requestRepository;
            this.staffRepository = staffRepository;
            this.shiftRepository = shiftRepository;
        }

        public Task<IReadOnlyList<int>> SimulateIncomingRequestsAsync(int count)
        {
            bool HasSpecializationAndLocation(DoctorProfile doctor) =>
                !string.IsNullOrWhiteSpace(doctor.Specialization) && !string.IsNullOrWhiteSpace(doctor.Location);

            (string Specialization, string Location) ToSpecializationLocationPair(DoctorProfile doctor) =>
                (doctor.Specialization.Trim(), doctor.Location.Trim());

            var availableDoctors = GetAvailableDoctors(GetDoctorRosterForDispatch());
            var liveTemplates = availableDoctors
                .Where(HasSpecializationAndLocation)
                .Select(ToSpecializationLocationPair)
                .Distinct()
                .ToArray();

            var templates = liveTemplates.Length > 0 ? liveTemplates : FallbackTemplates;
            var normalizedCount = Math.Max(1, count);
            var startIndex = DateTime.Now.Minute % templates.Length;
            var createdIds = new List<int>(normalizedCount);

            for (int templateIndex = 0; templateIndex < normalizedCount; templateIndex++)
            {
                var template = templates[(startIndex + templateIndex) % templates.Length];
                var newId = requestRepository.AddRequest(template.Specialization, template.Location, PendingStatus);
                createdIds.Add(newId);
            }

            return Task.FromResult<IReadOnlyList<int>>(createdIds);
        }

        public Task<IReadOnlyList<int>> GetPendingRequestIdsAsync()
        {
            int ToRequestId(ERRequest request) => request.Id;

            var pendingIds = GetPendingRequests()
                .Select(ToRequestId)
                .ToList();
            return Task.FromResult<IReadOnlyList<int>>(pendingIds);
        }

        public Task<ERDispatchResult> DispatchERRequestAsync(int requestId)
        {
            bool HasMatchingId(ERRequest pendingRequest) => pendingRequest.Id == requestId;
            var request = GetPendingRequests().FirstOrDefault(HasMatchingId);
            if (request == null)
            {
                return Task.FromResult(new ERDispatchResult
                {
                    IsSuccess = false,
                    Message = $"ER request #{requestId} not found or already processed.",
                });
            }

            var matchedDoctor = FindBestMatchingDoctor(request);

            if (matchedDoctor == null)
            {
                requestRepository.UpdateRequestStatus(requestId, UnmatchedStatus, null, null);
                return Task.FromResult(new ERDispatchResult
                {
                    Request = request,
                    IsSuccess = false,
                    Message = $"No AVAILABLE {request.Specialization} specialist found for {request.Location}.",
                });
            }

            requestRepository.UpdateRequestStatus(requestId, AssignedStatus, matchedDoctor.DoctorId, matchedDoctor.FullName);
            Task UpdateMatchedDoctorStatusAsync() =>
                staffRepository.UpdateStatusAsync(matchedDoctor.DoctorId, DoctorStatus.IN_EXAMINATION.ToString());
            Task.Run(UpdateMatchedDoctorStatusAsync).GetAwaiter().GetResult();

            return Task.FromResult(new ERDispatchResult
            {
                Request = request,
                MatchedDoctorId = matchedDoctor.DoctorId,
                MatchedDoctorName = matchedDoctor.FullName,
                MatchReason = $"Specialty match ({matchedDoctor.Specialization}) + AVAILABLE status + at {request.Location}",
                IsSuccess = true,
                Message = $"Assigned to {matchedDoctor.FullName}. Status changed to IN_EXAMINATION.",
            });
        }

        public Task<IReadOnlyList<DoctorProfile>> GetManualOverrideCandidatesAsync(int requestId, int nearEndMinutes)
        {
            var request = requestRepository.GetRequestById(requestId);
            if (request == null)
            {
                return Task.FromResult<IReadOnlyList<DoctorProfile>>(Array.Empty<DoctorProfile>());
            }

            var now = DateTime.Now;
            var inExaminationDoctors = GetDoctorsInExamination(GetDoctorRosterForDispatch());

            bool HasScheduleEnd(DoctorProfile doctor) => doctor.ScheduleEnd.HasValue;
            bool IsNearEnd(DoctorProfile doctor)
            {
                var minutesToEnd = (doctor.ScheduleEnd!.Value - now).TotalMinutes;
                return minutesToEnd >= 0 && minutesToEnd <= nearEndMinutes;
            }
            bool MatchesRequestSpecialization(DoctorProfile doctor) =>
                IsSameValue(doctor.Specialization, request.Specialization);

            int ByDoctorId(DoctorProfile doctor) => doctor.DoctorId;
            DoctorProfile FirstInGroup(IGrouping<int, DoctorProfile> doctorGroup) => doctorGroup.First();
            DateTime ByScheduleEndOrMax(DoctorProfile doctor) => doctor.ScheduleEnd ?? DateTime.MaxValue;
            string ByFullName(DoctorProfile doctor) => doctor.FullName;

            var candidates = inExaminationDoctors
                .Where(HasScheduleEnd)
                .Where(IsNearEnd)
                .Where(MatchesRequestSpecialization)
                .GroupBy(ByDoctorId)
                .Select(FirstInGroup)
                .OrderBy(ByScheduleEndOrMax)
                .ThenBy(ByFullName)
                .ToList();

            return Task.FromResult<IReadOnlyList<DoctorProfile>>(candidates);
        }

        public async Task<ERDispatchResult> ManualOverrideAsync(int requestId, int doctorId, int nearEndMinutes)
        {
            var request = requestRepository.GetRequestById(requestId);
            bool HasMatchingDoctorId(DoctorProfile rosterEntry) => rosterEntry.DoctorId == doctorId;
            var doctor = GetDoctorRosterForDispatch().FirstOrDefault(HasMatchingDoctorId);

            if (request == null || doctor == null)
            {
                return new ERDispatchResult
                {
                    IsSuccess = false,
                    Message = "Request or doctor not found.",
                };
            }

            var eligibleCandidates = await GetManualOverrideCandidatesAsync(requestId, nearEndMinutes);
            bool HasMatchingDoctorIdInCandidates(DoctorProfile overrideCandidate) => overrideCandidate.DoctorId == doctorId;
            if (!eligibleCandidates.Any(HasMatchingDoctorIdInCandidates))
            {
                return new ERDispatchResult
                {
                    Request = request,
                    IsSuccess = false,
                    Message = $"Manual override blocked. Doctor must be IN_EXAMINATION within {nearEndMinutes} min of end_time.",
                };
            }

            requestRepository.UpdateRequestStatus(requestId, AssignedStatus, doctor.DoctorId, doctor.FullName);
            await staffRepository.UpdateStatusAsync(doctor.DoctorId, DoctorStatus.IN_EXAMINATION.ToString());

            return new ERDispatchResult
            {
                Request = request,
                MatchedDoctorId = doctor.DoctorId,
                MatchedDoctorName = doctor.FullName,
                MatchReason = $"Manual override by administrator ({nearEndMinutes} min near end_time rule)",
                IsSuccess = true,
                Message = $"Manually assigned to {doctor.FullName}. Status changed to IN_EXAMINATION.",
            };
        }

        private DoctorProfile? FindBestMatchingDoctor(ERRequest request)
        {
            var availableDoctors = GetAvailableDoctors(GetDoctorRosterForDispatch());

            bool IsMatchingAvailableDoctor(DoctorProfile doctor) =>
                IsSameValue(doctor.Specialization, request.Specialization)
                && doctor.Status == DoctorStatus.AVAILABLE
                && IsSameValue(doctor.Location, request.Location);

            string ByFullName(DoctorProfile doctor) => doctor.FullName;

            return availableDoctors
                .Where(IsMatchingAvailableDoctor)
                .OrderBy(ByFullName)
                .FirstOrDefault();
        }

        private IReadOnlyList<DoctorProfile> GetDoctorRosterForDispatch()
        {
            var now = DateTime.Now;
            var allShifts = shiftRepository.GetAllShifts();
            var allStaff = staffRepository.LoadAllStaff();

            bool IsCurrentNonCancelledShift(Shift shift) =>
                shift.StartTime <= now
                && shift.EndTime >= now
                && shift.Status != ShiftStatus.CANCELLED
                && shift.Status != ShiftStatus.COMPLETED
                && shift.Status != ShiftStatus.VACATION;

            int ByAppointedStaffId(Shift shift) => shift.AppointedStaff.StaffID;
            int GroupKey(IGrouping<int, Shift> shiftGroup) => shiftGroup.Key;
            DateTime ByShiftEndTime(Shift shift) => shift.EndTime;
            Shift EarliestEndingShiftInGroup(IGrouping<int, Shift> shiftGroup) =>
                shiftGroup.OrderBy(ByShiftEndTime).First();

            var currentShiftsByStaffId = allShifts
                .Where(IsCurrentNonCancelledShift)
                .GroupBy(ByAppointedStaffId)
                .ToDictionary(GroupKey, EarliestEndingShiftInGroup);

            var roster = new List<DoctorProfile>();
            foreach (var staffMember in allStaff.OfType<Doctor>())
            {
                if (!currentShiftsByStaffId.TryGetValue(staffMember.StaffID, out var currentShift))
                {
                    continue;
                }

                roster.Add(new DoctorProfile
                {
                    DoctorId = staffMember.StaffID,
                    FullName = ($"{staffMember.FirstName} {staffMember.LastName}").Trim(),
                    Specialization = string.IsNullOrWhiteSpace(staffMember.Specialization) ? DefaultSpecialization : staffMember.Specialization.Trim(),
                    Status = staffMember.DoctorStatus,
                    Location = (currentShift.Location ?? string.Empty).Trim(),
                    ScheduleStart = currentShift.StartTime,
                    ScheduleEnd = currentShift.EndTime,
                });
            }
            return roster;
        }

        private IReadOnlyList<ERRequest> GetPendingRequests()
        {
            bool IsPending(ERRequest request) =>
                string.Equals((request.Status ?? string.Empty).Trim(), PendingStatus, StringComparison.OrdinalIgnoreCase);
            DateTime ByCreatedAt(ERRequest request) => request.CreatedAt;

            return requestRepository.GetAllRequests()
                .Where(IsPending)
                .OrderBy(ByCreatedAt)
                .ToList();
        }

        private static IReadOnlyList<DoctorProfile> GetAvailableDoctors(IEnumerable<DoctorProfile> roster)
        {
            bool IsAvailable(DoctorProfile doctor) => doctor.Status == DoctorStatus.AVAILABLE;
            return roster.Where(IsAvailable).ToList();
        }

        private static IReadOnlyList<DoctorProfile> GetDoctorsInExamination(IEnumerable<DoctorProfile> roster)
        {
            bool IsInExamination(DoctorProfile doctor) => doctor.Status == DoctorStatus.IN_EXAMINATION;
            return roster.Where(IsInExamination).ToList();
        }

        private static bool IsSameValue(string leftOperator, string rightOperator) =>
            string.Equals((leftOperator ?? string.Empty).Trim(), (rightOperator ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
