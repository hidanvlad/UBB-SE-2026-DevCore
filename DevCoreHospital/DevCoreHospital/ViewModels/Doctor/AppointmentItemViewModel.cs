using DevCoreHospital.Models;

namespace DevCoreHospital.ViewModels.Doctor;

public class AppointmentItemViewModel
{
    public int Id { get; }
    public string TimeRange { get; }
    public string Location { get; }
    public string Type { get; }
    public string Status { get; }
    public string StatusBrush { get; } // XAML color string

    public AppointmentItemViewModel(Appointment a)
    {
        Id = a.Id;
        TimeRange = $"{a.StartTime:hh\\:mm} - {a.EndTime:hh\\:mm}";
        Location = a.Location;
        Type = a.Type;
        Status = a.Status;
        StatusBrush = ToStatusBrush(a.Status);
    }

    private static string ToStatusBrush(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "scheduled" => "#2563EB",
            "confirmed" => "#059669",
            "cancelled" => "#DC2626",
            "completed" => "#6B7280",
            _ => "#7C3AED"
        };
    }
}