using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public sealed class ERDispatchService : IERDispatchService
    {
        private readonly IERDispatchRepository repository;

        public ERDispatchService(IERDispatchRepository repository)
        {
            this.repository = repository;
        }

        public Task<IReadOnlyList<int>> SimulateIncomingRequestsAsync(int count)
        {
            bool HasSpecializationAndLocation(DoctorProfile doctor) =>
                !string.IsNullOrWhiteSpace(doctor.Specialization) && !string.IsNullOrWhiteSpace(doctor.Location);

            var liveTemplates = GetAvailableDoctors(GetDoctorRosterForDispatch())
                .Where(HasSpecializationAndLocation)
                .Select(doctor => (Specialization: doctor.Specialization.Trim(), Location: doctor.Location.Trim()))
                .Distinct()
                .ToArray();

            var fallbackTemplates = new (string Specialization, string Location)[]
            {
                ("Surgeon", "Ward A"),
                ("Cardiology", "Ward A"),
                ("Neurology", "Ward A"),
                ("Pediatrics", "Ward A")
            };

            var templates = liveTemplates.Length > 0 ? liveTemplates : fallbackTemplates;

            var normalizedCount = Math.Max(1, count);
            var startIndex = DateTime.Now.Minute % templates.Length;
            var createdIds = new List<int>(normalizedCount);

            for (int templateIndex = 0; templateIndex < normalizedCount; templateIndex++)
            {
                var template = templates[(startIndex + templateIndex) % templates.Length];
                var newId = repository.CreateIncomingRequest(template.Specialization, template.Location);
                createdIds.Add(newId);
            }

            return Task.FromResult<IReadOnlyList<int>>(createdIds);
        }

        public Task<IReadOnlyList<int>> GetPendingRequestIdsAsync()
        {
            var ids = GetPendingRequests()
                .Select(request => request.Id)
                .ToList();

            return Task.FromResult<IReadOnlyList<int>>(ids);
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
                    Message = $"ER request #{requestId} not found or already processed."
                });
            }

            var matchedDoctor = FindBestMatchingDoctor(request);

            if (matchedDoctor == null)
            {
                var result = new ERDispatchResult
                {
                    Request = request,
                    IsSuccess = false,
                    Message = $"No AVAILABLE {request.Specialization} specialist found for {request.Location}."
                };

                repository.UpdateRequestStatus(requestId, "UNMATCHED", null, null);

                return Task.FromResult(result);
            }

            repository.UpdateRequestStatus(requestId, "ASSIGNED", matchedDoctor.DoctorId, matchedDoctor.FullName);
            repository.UpdateDoctorStatus(matchedDoctor.DoctorId, DoctorStatus.IN_EXAMINATION);

            return Task.FromResult(new ERDispatchResult
            {
                Request = request,
                MatchedDoctorId = matchedDoctor.DoctorId,
                MatchedDoctorName = matchedDoctor.FullName,
                MatchReason = $"Specialty match ({matchedDoctor.Specialization}) + AVAILABLE status + at {request.Location}",
                IsSuccess = true,
                Message = $"Assigned to {matchedDoctor.FullName}. Status changed to IN_EXAMINATION."
            });
        }

        public Task<IReadOnlyList<DoctorProfile>> GetManualOverrideCandidatesAsync(int requestId, int nearEndMinutes)
        {
            var request = repository.GetRequestById(requestId);
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

            var nearEndInExamination = inExaminationDoctors
                .Where(HasScheduleEnd)
                .Where(IsNearEnd)
                .ToList();

            bool MatchesRequestSpecialization(DoctorProfile doctor) => IsSameValue(doctor.Specialization, request.Specialization);

            var strictCandidates = nearEndInExamination
                .GroupBy(doctor => doctor.DoctorId)
                .Select(doctorGroup => doctorGroup.First())
                .Where(MatchesRequestSpecialization)
                .ToList();

            var candidates = strictCandidates
                .GroupBy(doctor => doctor.DoctorId)
                .Select(doctorGroup => doctorGroup.First())
                .OrderByDescending(doctor => IsSameValue(doctor.Specialization, request.Specialization))
                .ThenByDescending(doctor => IsSameValue(doctor.Location, request.Location))
                .ThenBy(doctor => doctor.ScheduleEnd ?? DateTime.MaxValue)
                .ThenBy(doctor => doctor.FullName)
                .ToList();

            return Task.FromResult<IReadOnlyList<DoctorProfile>>(candidates);
        }

        public async Task<ERDispatchResult> ManualOverrideAsync(int requestId, int doctorId, int nearEndMinutes)
        {
            var request = repository.GetRequestById(requestId);
            bool HasMatchingDoctorId(DoctorRosterEntry rosterEntry) => rosterEntry.DoctorId == doctorId;
            var doctorRosterEntry = GetDoctorRosterForDispatch().FirstOrDefault(HasMatchingDoctorId);
            var doctor = doctorRosterEntry == null ? null : ToDoctorProfile(doctorRosterEntry);

            if (request == null || doctor == null)
            {
                return new ERDispatchResult
                {
                    IsSuccess = false,
                    Message = "Request or doctor not found."
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
                    Message =
                        $"Manual override blocked. Doctor must be IN_EXAMINATION within {nearEndMinutes} min of end_time."
                };
            }

            repository.UpdateRequestStatus(requestId, "ASSIGNED", doctor.DoctorId, doctor.FullName);
            repository.UpdateDoctorStatus(doctor.DoctorId, DoctorStatus.IN_EXAMINATION);

            return new ERDispatchResult
            {
                Request = request,
                MatchedDoctorId = doctor.DoctorId,
                MatchedDoctorName = doctor.FullName,
                MatchReason = $"Manual override by administrator ({nearEndMinutes} min near end_time rule)",
                IsSuccess = true,
                Message = $"Manually assigned to {doctor.FullName}. Status changed to IN_EXAMINATION."
            };
        }

        private DoctorProfile? FindBestMatchingDoctor(ERRequest request)
        {
            var availableDoctors = GetAvailableDoctors(GetDoctorRosterForDispatch());

            bool IsMatchingAvailableDoctor(DoctorProfile doctor) =>
                IsSameValue(doctor.Specialization, request.Specialization) &&
                doctor.Status == DoctorStatus.AVAILABLE &&
                IsSameValue(doctor.Location, request.Location);

            var matches = availableDoctors
                .Where(IsMatchingAvailableDoctor)
                .OrderBy(doctor => doctor.FullName)
                .ToList();

            return matches.FirstOrDefault();
        }

        private IReadOnlyList<DoctorRosterEntry> GetDoctorRosterForDispatch()
        {
            var now = DateTime.Now;

            DateTime GetScheduleEnd(DoctorRosterEntry rosterEntry) => rosterEntry.ScheduleEnd ?? DateTime.MaxValue;

            return repository.GetDoctorRoster()
                .Where(IsDoctor)
                .Where(entry => IsOnCurrentShift(entry, now))
                .Select(NormalizeRosterEntry)
                .GroupBy(entry => entry.DoctorId)
                .Select(group => group.OrderBy(GetScheduleEnd).First())
                .ToList();
        }

        private IReadOnlyList<ERRequest> GetPendingRequests()
        {
            return repository.GetPendingRequests()
                .Where(request => string.Equals(NormalizeToken(request.Status), "PENDING", StringComparison.OrdinalIgnoreCase))
                .OrderBy(request => request.CreatedAt)
                .ToList();
        }

        private static IReadOnlyList<DoctorProfile> GetAvailableDoctors(IEnumerable<DoctorRosterEntry> roster)
            => GetDoctorsByStatus(roster, DoctorStatus.AVAILABLE);

        private static IReadOnlyList<DoctorProfile> GetDoctorsInExamination(IEnumerable<DoctorRosterEntry> roster)
            => GetDoctorsByStatus(roster, DoctorStatus.IN_EXAMINATION);

        private static DoctorRosterEntry NormalizeRosterEntry(DoctorRosterEntry entry)
        {
            return new DoctorRosterEntry
            {
                DoctorId = entry.DoctorId,
                FullName = NormalizeToken(entry.FullName),
                RoleRaw = NormalizeToken(entry.RoleRaw),
                Specialization = string.IsNullOrWhiteSpace(entry.Specialization) ? "General" : entry.Specialization.Trim(),
                StatusRaw = string.IsNullOrWhiteSpace(entry.StatusRaw) ? "OFF_DUTY" : entry.StatusRaw.Trim(),
                Location = NormalizeToken(entry.Location),
                IsShiftActive = entry.IsShiftActive,
                ShiftStatusRaw = NormalizeToken(entry.ShiftStatusRaw),
                ScheduleStart = entry.ScheduleStart,
                ScheduleEnd = entry.ScheduleEnd,
            };
        }

        private static bool IsDoctor(DoctorRosterEntry entry)
        {
            var normalizedRole = NormalizeToken(entry.RoleRaw);
            if (string.IsNullOrEmpty(normalizedRole))
            {
                return true;
            }

            return string.Equals(normalizedRole, "DOCTOR", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOnCurrentShift(DoctorRosterEntry entry, DateTime now)
        {
            if (!entry.ScheduleStart.HasValue || !entry.ScheduleEnd.HasValue)
            {
                return false;
            }

            if (entry.ScheduleStart.Value > now || entry.ScheduleEnd.Value < now)
            {
                return false;
            }

            if (entry.IsShiftActive.HasValue && !entry.IsShiftActive.Value)
            {
                return false;
            }

            var shiftStatus = NormalizeToken(entry.ShiftStatusRaw);
            if (string.Equals(shiftStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shiftStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shiftStatus, "VACATION", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static DoctorProfile ToDoctorProfile(DoctorRosterEntry entry)
        {
            return new DoctorProfile
            {
                DoctorId = entry.DoctorId,
                FullName = NormalizeToken(entry.FullName),
                Specialization = string.IsNullOrWhiteSpace(entry.Specialization) ? "General" : entry.Specialization.Trim(),
                Status = ParseStatus(entry.StatusRaw),
                Location = NormalizeToken(entry.Location),
                ScheduleStart = entry.ScheduleStart,
                ScheduleEnd = entry.ScheduleEnd
            };
        }

        private static IReadOnlyList<DoctorProfile> GetDoctorsByStatus(IEnumerable<DoctorRosterEntry> roster, DoctorStatus targetStatus)
        {
            bool HasTargetStatus(DoctorProfile doctorProfile) => doctorProfile.Status == targetStatus;

            return roster
                .Select(ToDoctorProfile)
                .Where(HasTargetStatus)
                .ToList();
        }

        private static DoctorStatus ParseStatus(string? raw)
        {
            var token = NormalizeToken(raw).Replace(" ", "_");

            if (string.Equals(token, "Available", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
            {
                token = nameof(DoctorStatus.AVAILABLE);
            }

            return Enum.TryParse<DoctorStatus>(token, true, out var status)
                ? status
                : DoctorStatus.OFF_DUTY;
        }

        private static bool IsSameValue(string left, string right)
        {
            return string.Equals(NormalizeToken(left), NormalizeToken(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeToken(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
