using System;

namespace DevCoreHospital.Models;

public class Appointment
{
    public int Id { get; set; }
    public int DoctorId { get; set; }

    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public DateTime StartDateTime => Date.Date + StartTime;
    public DateTime EndDateTime => Date.Date + EndTime;
}