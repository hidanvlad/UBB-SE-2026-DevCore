using System;

namespace DevCoreHospital.Models;

/// <summary>
/// A pharmacist work shift including rotation assignment and lifecycle status.
/// </summary>
public class PharmacyShift
{
    public string Id { get; set; } = string.Empty;
    public string PharmacistStaffId { get; set; } = string.Empty;
    public string RotationAssignment { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    /// <summary>Scheduled, Active, or Completed (case-insensitive in storage).</summary>
    public string Status { get; set; } = "Scheduled";
}
