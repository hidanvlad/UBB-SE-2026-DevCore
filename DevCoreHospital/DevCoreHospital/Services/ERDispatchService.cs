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
            var candidates = _dataSource.GetDoctorsInExamination()
                .Where(d => string.Equals(d.Specialization, request.Specialization, StringComparison.OrdinalIgnoreCase))
                .Where(d => string.Equals(d.Location, request.Location, StringComparison.OrdinalIgnoreCase))
                .Where(d => d.ScheduleEnd.HasValue)
                .Where(d =>
                {
                    var minutesToEnd = (d.ScheduleEnd!.Value - now).TotalMinutes;
                    return minutesToEnd >= 0 && minutesToEnd <= nearEndMinutes;
                })
                .OrderBy(d => d.ScheduleEnd)
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
                        $"Manual override blocked. Doctor must be IN_EXAMINATION and within {nearEndMinutes} min of end_time."
                };
            }

            _dataSource.UpdateRequestStatus(requestId, "ASSIGNED", doctor.DoctorId, doctor.FullName);

            return new ERDispatchResult
            {
                Request = request,
                MatchedDoctorId = doctor.DoctorId,
                MatchedDoctorName = doctor.FullName,
                MatchReason = $"Manual override by administrator ({nearEndMinutes} min near end_time rule)",
                IsSuccess = true,
                Message = $"Manually assigned to {doctor.FullName}."
            };
        }

        private DoctorProfile? FindBestMatchingDoctor(ERRequest request)
        {
            var availableDoctors = _dataSource.GetAvailableDoctors();

            var matches = availableDoctors
                .Where(d =>
                    string.Equals(d.Specialization, request.Specialization, StringComparison.OrdinalIgnoreCase) &&
                    d.Status == DoctorStatus.AVAILABLE &&
                    string.Equals(d.Location, request.Location, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.FullName)
                .ToList();

            return matches.FirstOrDefault();
        }
    }
}

