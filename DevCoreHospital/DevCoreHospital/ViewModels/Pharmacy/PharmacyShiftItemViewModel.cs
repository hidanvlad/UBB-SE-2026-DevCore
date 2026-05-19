using System;
using System.Globalization;
using DevCoreHospital.Models;

namespace DevCoreHospital.ViewModels.Pharmacy;

public sealed class PharmacyShiftItemViewModel
{
    private const string EnglishCultureCode = "en-US";
    private const string TimeFormat = "HH:mm";
    private const string DayLabelFormat = "ddd, dd MMM yyyy";

    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo(EnglishCultureCode);

    public PharmacyShiftItemViewModel(Shift shift)
    {
        RotationAssignment = shift.Location;
        ShiftStartTime = shift.StartTime;
        ShiftEndTime = shift.EndTime;
        Status = shift.Status;
    }

    public string RotationAssignment { get; }
    public DateTime ShiftStartTime { get; }
    public DateTime? ShiftEndTime { get; }

    public string ShiftStartTimeText => ShiftStartTime.ToString(TimeFormat);
    public string ShiftEndTimeText => ShiftEndTime.HasValue ? ShiftEndTime.Value.ToString(TimeFormat) : "—";
    public string DayLabel => ShiftStartTime.ToString(DayLabelFormat, EnglishCulture);

    public string TimeRangeDetail =>
        $"Shift start: {ShiftStartTimeText}  ·  Shift end: {ShiftEndTimeText}  ·  Duration: {DurationText}";

    public string DurationText
    {
        get
        {
            if (!ShiftEndTime.HasValue)
            {
                return "Open-ended";
            }

            var span = ShiftEndTime.Value - ShiftStartTime;
            if (span.TotalMinutes <= 0)
            {
                return "—";
            }

            return $"{(int)span.TotalHours}h {span.Minutes}m";
        }
    }

    public string StatusDisplay
    {
        get
        {
            return Status switch
            {
                ShiftStatus.SCHEDULED => "Scheduled",
                ShiftStatus.ACTIVE => "Active",
                ShiftStatus.COMPLETED => "Completed",
                ShiftStatus.CANCELLED => "Cancelled",
                _ => Status.ToString(),
            };
        }
    }

    private ShiftStatus Status { get; }
}
