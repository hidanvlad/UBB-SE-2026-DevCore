using System;

namespace DevCoreHospital.Models
{

    public sealed class ERRequest
    {
        public int Id { get; set; }
        public string Specialization { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "PENDING";  // PENDING, ASSIGNED, COMPLETED
        public int? AssignedDoctorId { get; set; }
        public string? AssignedDoctorName { get; set; }
    }

    public sealed class ERDispatchResult
    {
        public ERRequest Request { get; set; } = new();
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
    }
}

