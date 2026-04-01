using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public sealed class ERDispatchService : IERDispatchService
    {
        private readonly IERDispatchRepository _repository;

        public ERDispatchService(IERDispatchRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<int>> SimulateIncomingRequestsAsync(int count)
        {
            var liveTemplates = GetAvailableDoctors(_repository.GetDoctorRoster())
                .Where(d => !string.IsNullOrWhiteSpace(d.Specialization) && !string.IsNullOrWhiteSpace(d.Location))
                .Select(d => (Specialization: d.Specialization.Trim(), Location: d.Location.Trim()))
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

            for (int i = 0; i < normalizedCount; i++)
            {
                var template = templates[(startIndex + i) % templates.Length];
                var newId = _repository.CreateIncomingRequest(template.Specialization, template.Location);
                createdIds.Add(newId);
            }

            return Task.FromResult<IReadOnlyList<int>>(createdIds);
        }

        public Task<IReadOnlyList<int>> GetPendingRequestIdsAsync()
        {
            var ids = _repository.GetPendingRequests()
                .Select(request => request.Id)
                .ToList();

            return Task.FromResult<IReadOnlyList<int>>(ids);
        }

        public Task<ERDispatchResult> DispatchERRequestAsync(int requestId)
        {
            var request = _repository.GetPendingRequests().FirstOrDefault(r => r.Id == requestId);
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

                _repository.UpdateRequestStatus(requestId, "UNMATCHED", null, null);

                return Task.FromResult(result);
            }

            _repository.UpdateRequestStatus(requestId, "ASSIGNED", matchedDoctor.DoctorId, matchedDoctor.FullName);
            _repository.UpdateDoctorStatus(matchedDoctor.DoctorId, DoctorStatus.IN_EXAMINATION);

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
            var request = _repository.GetRequestById(requestId);
            if (request == null)
                return Task.FromResult<IReadOnlyList<DoctorProfile>>(Array.Empty<DoctorProfile>());

            var now = DateTime.Now;
            var inExamDoctors = GetDoctorsInExamination(_repository.GetDoctorRoster());

            var nearEndInExam = inExamDoctors
                .Where(d => d.ScheduleEnd.HasValue)
                .Where(d =>
                {
                    var minutesToEnd = (d.ScheduleEnd!.Value - now).TotalMinutes;
                    return minutesToEnd >= 0 && minutesToEnd <= nearEndMinutes;
                })
                .ToList();

            var strictCandidates = nearEndInExam
                .GroupBy(d => d.DoctorId)
                .Select(g => g.First())
                .Where(d => IsSameValue(d.Specialization, request.Specialization))
                .ToList();

            var candidates = strictCandidates
                .GroupBy(d => d.DoctorId)
                .Select(g => g.First())
                .OrderByDescending(d => IsSameValue(d.Specialization, request.Specialization))
                .ThenByDescending(d => IsSameValue(d.Location, request.Location))
                .ThenBy(d => d.ScheduleEnd ?? DateTime.MaxValue)
                .ThenBy(d => d.FullName)
                .ToList();

            return Task.FromResult<IReadOnlyList<DoctorProfile>>(candidates);
        }

        public async Task<ERDispatchResult> ManualOverrideAsync(int requestId, int doctorId, int nearEndMinutes)
        {
            var request = _repository.GetRequestById(requestId);
            var doctorRow = _repository.GetDoctorById(doctorId);
            var doctor = doctorRow == null ? null : ToDoctorProfile(doctorRow);

            if (request == null || doctor == null)
            {
                return new ERDispatchResult
                {
                    IsSuccess = false,
                    Message = "Request or doctor not found."
                };
            }

            var eligible = await GetManualOverrideCandidatesAsync(requestId, nearEndMinutes);
            if (!eligible.Any(d => d.DoctorId == doctorId))
            {
                return new ERDispatchResult
                {
                    Request = request,
                    IsSuccess = false,
                    Message =
                        $"Manual override blocked. Doctor must be IN_EXAMINATION within {nearEndMinutes} min of end_time."
                };
            }

            _repository.UpdateRequestStatus(requestId, "ASSIGNED", doctor.DoctorId, doctor.FullName);
            _repository.UpdateDoctorStatus(doctor.DoctorId, DoctorStatus.IN_EXAMINATION);

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
            var availableDoctors = GetAvailableDoctors(_repository.GetDoctorRoster());

            var matches = availableDoctors
                .Where(d =>
                    IsSameValue(d.Specialization, request.Specialization) &&
                    d.Status == DoctorStatus.AVAILABLE &&
                    IsSameValue(d.Location, request.Location))
                .OrderBy(d => d.FullName)
                .ToList();

            return matches.FirstOrDefault();
        }

        private static IReadOnlyList<DoctorProfile> GetAvailableDoctors(IEnumerable<DoctorRosterEntry> roster)
            => GetDoctorsByStatus(roster, DoctorStatus.AVAILABLE);

        private static IReadOnlyList<DoctorProfile> GetDoctorsInExamination(IEnumerable<DoctorRosterEntry> roster)
            => GetDoctorsByStatus(roster, DoctorStatus.IN_EXAMINATION);

        private static DoctorProfile ToDoctorProfile(DoctorRosterEntry entry)
        {
            return new DoctorProfile
            {
                DoctorId = entry.DoctorId,
                FullName = entry.FullName,
                Specialization = entry.Specialization,
                Status = ParseStatus(entry.StatusRaw),
                Location = entry.Location,
                ScheduleStart = entry.ScheduleStart,
                ScheduleEnd = entry.ScheduleEnd
            };
        }

        private static IReadOnlyList<DoctorProfile> GetDoctorsByStatus(IEnumerable<DoctorRosterEntry> roster, DoctorStatus status)
        {
            return roster
                .Select(ToDoctorProfile)
                .Where(IsOnCurrentShift)
                .Where(profile => profile.Status == status)
                .ToList();
        }

        private static bool IsOnCurrentShift(DoctorProfile profile)
        {
            return profile.ScheduleStart.HasValue && profile.ScheduleEnd.HasValue;
        }

        private static DoctorStatus ParseStatus(string? raw)
        {
            var token = (raw ?? string.Empty).Trim().Replace(" ", "_");

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

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
