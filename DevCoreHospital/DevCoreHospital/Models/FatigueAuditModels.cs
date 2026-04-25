using System.Collections.Generic;
using System;

namespace DevCoreHospital.Models
{
    public sealed class RosterShift
    {
        public int Id { get; set; }
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string? Status { get; set; }
    }

    public sealed class StaffProfile
    {
        public int StaffId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public bool? IsAvailable { get; set; }
        public bool? IsActive { get; set; }
        public string? Status { get; set; }
    }

    public sealed class AuditViolation
    {
        public int ShiftId { get; set; }
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public DateTime ShiftStart { get; set; }
        public DateTime ShiftEnd { get; set; }
        public string Rule { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class AutoSuggestRecommendation
    {
        public int ShiftId { get; set; }
        public int OriginalStaffId { get; set; }
        public string OriginalStaffName { get; set; } = string.Empty;
        public int? SuggestedStaffId { get; set; }
        public string SuggestedStaffName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class AutoAuditResult
    {
        public DateTime WeekStart { get; set; }
        public bool HasConflicts { get; set; }
        public bool CanPublish => !HasConflicts;
        public string Summary { get; set; } = string.Empty;

        public IReadOnlyList<AuditViolation> Violations { get; set; } = Array.Empty<AuditViolation>();
        public IReadOnlyList<AutoSuggestRecommendation> Suggestions { get; set; } = Array.Empty<AutoSuggestRecommendation>();
    }
}
