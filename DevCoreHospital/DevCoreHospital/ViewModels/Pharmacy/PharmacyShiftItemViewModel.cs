using System;
using DevCoreHospital.Models;

namespace DevCoreHospital.ViewModels.Pharmacy;

public sealed class PharmacyShiftItemViewModel
{
    public PharmacyShiftItemViewModel(PharmacyShift shift)
    {
        RotationAssignment = shift.RotationAssignment;
        ShiftStartTime = shift.StartTime;
        ShiftEndTime = shift.EndTime;
        StatusRaw = shift.Status?.Trim() ?? string.Empty;
    }

    public string RotationAssignment { get; }
    public DateTime ShiftStartTime { get; }
    public DateTime? ShiftEndTime { get; }

    public string ShiftStartTimeText => ShiftStartTime.ToString("HH:mm");
    public string ShiftEndTimeText => ShiftEndTime.HasValue ? ShiftEndTime.Value.ToString("HH:mm") : "—";
    public string DayLabel => ShiftStartTime.ToString("ddd, dd MMM yyyy");

    public string TimeRangeDetail =>
        $"Shift start: {ShiftStartTimeText}  ·  Shift end: {ShiftEndTimeText}  ·  Duration: {DurationText}";

    public string DurationText
    {
        get
        {
            if (!ShiftEndTime.HasValue)
                return "Open-ended";
            var span = ShiftEndTime.Value - ShiftStartTime;
            if (span.TotalMinutes <= 0)
                return "—";
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        }
    }

    /// <summary>Scheduled, Active, or Completed for UI.</summary>
    public string StatusDisplay
    {
        get
        {
            if (string.Equals(StatusRaw, "Scheduled", StringComparison.OrdinalIgnoreCase))
                return "Scheduled";
            if (string.Equals(StatusRaw, "Active", StringComparison.OrdinalIgnoreCase))
                return "Active";
            if (string.Equals(StatusRaw, "Completed", StringComparison.OrdinalIgnoreCase))
                return "Completed";
            return string.IsNullOrEmpty(StatusRaw) ? "Scheduled" : StatusRaw;
        }
    }

    private string StatusRaw { get; }
}
