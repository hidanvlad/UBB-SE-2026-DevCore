using DevCoreHospital.Data;
using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DevCoreHospital.Repositories
{
    public sealed class ERDispatchRepository : IERDispatchRepository
    {
        private readonly IERDispatchDataSource _dataSource;

        public ERDispatchRepository(IERDispatchDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public IReadOnlyList<DoctorRosterEntry> GetDoctorRoster()
        {
            var now = DateTime.Now;

            return _dataSource.GetRosterEntries()
                .Where(IsDoctor)
                .Where(entry => IsOnCurrentShift(entry, now))
                .Select(NormalizeRosterEntry)
                .GroupBy(entry => entry.DoctorId)
                .Select(group => group.OrderBy(e => e.ScheduleEnd ?? DateTime.MaxValue).First())
                .ToList();
        }

        public IReadOnlyList<ERRequest> GetPendingRequests()
        {
            return _dataSource.GetRequests()
                .Where(request => string.Equals(Normalize(request.Status), "PENDING", StringComparison.OrdinalIgnoreCase))
                .OrderBy(request => request.CreatedAt)
                .ToList();
        }

        public int CreateIncomingRequest(string specialization, string location)
            => _dataSource.CreateRequest(specialization, location, "PENDING");

        public ERRequest? GetRequestById(int requestId) => _dataSource.GetRequestById(requestId);

        public DoctorRosterEntry? GetDoctorById(int doctorId)
        {
            var now = DateTime.Now;

            return _dataSource.GetRosterEntriesByStaffId(doctorId)
                .Where(IsDoctor)
                .Where(entry => IsOnCurrentShift(entry, now))
                .Select(NormalizeRosterEntry)
                .OrderBy(entry => entry.ScheduleEnd ?? DateTime.MaxValue)
                .FirstOrDefault();
        }

        public void UpdateRequestStatus(int requestId, string status, int? doctorId, string? doctorName)
            => _dataSource.UpdateRequestStatus(requestId, status, doctorId, doctorName);

        public void UpdateDoctorStatus(int doctorId, DoctorStatus status)
            => _dataSource.UpdateDoctorStatus(doctorId, status);

        private static DoctorRosterEntry NormalizeRosterEntry(DoctorRosterEntry entry)
        {
            return new DoctorRosterEntry
            {
                DoctorId = entry.DoctorId,
                FullName = Normalize(entry.FullName),
                RoleRaw = Normalize(entry.RoleRaw),
                Specialization = string.IsNullOrWhiteSpace(entry.Specialization) ? "General" : entry.Specialization.Trim(),
                StatusRaw = string.IsNullOrWhiteSpace(entry.StatusRaw) ? "OFF_DUTY" : entry.StatusRaw.Trim(),
                Location = Normalize(entry.Location),
                IsShiftActive = entry.IsShiftActive,
                ShiftStatusRaw = Normalize(entry.ShiftStatusRaw),
                ScheduleStart = entry.ScheduleStart,
                ScheduleEnd = entry.ScheduleEnd
            };
        }

        private static bool IsDoctor(DoctorRosterEntry entry)
        {
            return string.Equals(Normalize(entry.RoleRaw), "DOCTOR", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOnCurrentShift(DoctorRosterEntry entry, DateTime now)
        {
            if (!entry.ScheduleStart.HasValue || !entry.ScheduleEnd.HasValue)
                return false;

            if (entry.ScheduleStart.Value > now || entry.ScheduleEnd.Value < now)
                return false;

            if (entry.IsShiftActive.HasValue && !entry.IsShiftActive.Value)
                return false;

            var shiftStatus = Normalize(entry.ShiftStatusRaw);
            if (string.Equals(shiftStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shiftStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shiftStatus, "VACATION", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}

