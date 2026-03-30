using DevCoreHospital.Data;
using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public sealed class ERDispatchService : IERDispatchService
    {
        private readonly IERDispatchDataSource _dataSource;

        public ERDispatchService(IERDispatchDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public Task<IReadOnlyList<int>> SimulateIncomingRequestsAsync(int count)
        {
            var templates = new (string Specialization, string Location)[]
            {
                ("Surgeon", "Ward A"),
                ("Cardiologist", "Ward A"),
                ("Neurology", "Ward A"),
                ("Pediatrics", "Ward A")
            };

            var normalizedCount = Math.Max(1, count);
            var startIndex = DateTime.Now.Minute % templates.Length;
            var createdIds = new List<int>(normalizedCount);

            for (int i = 0; i < normalizedCount; i++)
            {
                var template = templates[(startIndex + i) % templates.Length];
                var newId = _dataSource.CreateIncomingRequest(template.Specialization, template.Location);
                createdIds.Add(newId);
            }

            return Task.FromResult<IReadOnlyList<int>>(createdIds);
        }

        public Task<IReadOnlyList<int>> GetPendingRequestIdsAsync()
        {
            var ids = _dataSource.GetPendingRequests()
                .Select(request => request.Id)
                .ToList();

            return Task.FromResult<IReadOnlyList<int>>(ids);
        }

        public async Task<ERDispatchResult> DispatchERRequestAsync(int requestId)
        {
            var request = _dataSource.GetPendingRequests().FirstOrDefault(r => r.Id == requestId);
            if (request == null)
            {
                return new ERDispatchResult
                {
                    IsSuccess = false,
                    Message = $"ER request #{requestId} not found or already processed."
                };
            }

            var matchedDoctor = FindBestMatchingDoctor(request);

            if (matchedDoctor == null)
            {
                var result = new ERDispatchResult
                {
                    Request = request,
                    IsSuccess = false,
                    Message = $"No available {request.Specialization} specialist found for {request.Location}."
                };

                // Flag in red: system stores as unmatched, admin sees alert
                _dataSource.UpdateRequestStatus(requestId, "UNMATCHED", null, null);

                return result;
            }

            // Successful match: update request + doctor status
            _dataSource.UpdateRequestStatus(requestId, "ASSIGNED", matchedDoctor.DoctorId, matchedDoctor.FullName);
            _dataSource.UpdateDoctorStatus(matchedDoctor.DoctorId, DoctorStatus.IN_EXAMINATION);

            return new ERDispatchResult
            {
                Request = request,
                MatchedDoctorId = matchedDoctor.DoctorId,
                MatchedDoctorName = matchedDoctor.FullName,
                MatchReason = $"Specialty match ({matchedDoctor.Specialization}) + AVAILABLE status + at {request.Location}",
                IsSuccess = true,
                Message = $"Assigned to {matchedDoctor.FullName}. Status changed to IN_EXAMINATION."
            };
        }

        public async Task<IReadOnlyList<DoctorProfile>> GetManualOverrideCandidatesAsync(int requestId, int nearEndMinutes)
        {
            var request = _dataSource.GetRequestById(requestId);
            if (request == null)
                return Array.Empty<DoctorProfile>();

            var now = DateTime.Now;
            var nearEndInExam = _dataSource.GetDoctorsInExamination()
                .Where(d => d.ScheduleEnd.HasValue)
                .Where(d =>
                {
                    var minutesToEnd = (d.ScheduleEnd!.Value - now).TotalMinutes;
                    return minutesToEnd >= 0 && minutesToEnd <= nearEndMinutes;
                })
                .ToList();

            var notWorking = _dataSource.GetDoctorsNotWorkingNow()
                .ToList();

            var strictCandidates = nearEndInExam
                .Concat(notWorking)
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

            return candidates;
        }

        public async Task<ERDispatchResult> ManualOverrideAsync(int requestId, int doctorId, int nearEndMinutes)
        {
            var request = _dataSource.GetRequestById(requestId);
            var doctor = _dataSource.GetDoctorById(doctorId);

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
                        $"Manual override blocked. Doctor must be IN_EXAMINATION within {nearEndMinutes} min of end_time or not currently working."
                };
            }

            _dataSource.UpdateRequestStatus(requestId, "ASSIGNED", doctor.DoctorId, doctor.FullName);
            _dataSource.UpdateDoctorStatus(doctor.DoctorId, DoctorStatus.IN_EXAMINATION);

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
            var availableDoctors = _dataSource.GetAvailableDoctors();

            var matches = availableDoctors
                .Where(d =>
                    IsSameValue(d.Specialization, request.Specialization) &&
                    d.Status == DoctorStatus.AVAILABLE &&
                    IsSameValue(d.Location, request.Location))
                .OrderBy(d => d.FullName)
                .ToList();

            return matches.FirstOrDefault();
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

