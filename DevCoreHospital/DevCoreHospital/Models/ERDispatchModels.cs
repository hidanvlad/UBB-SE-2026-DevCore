using System;

namespace DevCoreHospital.Models
{
    public sealed class ERRequest
    {
        public const string PendingStatus = "PENDING";

        public int Id { get; set; }
        public string Specialization { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = PendingStatus;
        public int? AssignedDoctorId { get; set; }
        public string? AssignedDoctorName { get; set; }
    }

    public sealed class ERDispatchResult
    {
        public ERRequest Request { get; set; } = new ERRequest();
        public int? MatchedDoctorId { get; set; }
        public string? MatchedDoctorName { get; set; }
        public string MatchReason { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class DoctorProfile
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public DoctorStatus Status { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime? ScheduleStart { get; set; }
        public DateTime? ScheduleEnd { get; set; }

        public int MinutesToEnd => ScheduleEnd.HasValue
            ? Math.Max(0, (int)Math.Round((ScheduleEnd.Value - DateTime.Now).TotalMinutes))
            : -1;
    }

    public sealed class DoctorRosterEntry
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string RoleRaw { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string StatusRaw { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool? IsShiftActive { get; set; }
        public string ShiftStatusRaw { get; set; } = string.Empty;
        public DateTime? ScheduleStart { get; set; }
        public DateTime? ScheduleEnd { get; set; }
    }
}

