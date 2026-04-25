using System;
using DevCoreHospital.Models;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class AppointmentItemViewModel
    {
        public int Id { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string DateText => Date.ToString("dd MMM yyyy");
        public string Notes { get; set; } = string.Empty;

        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public string TimeRangeText => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
        public string LocationSafe => string.IsNullOrWhiteSpace(Location) ? "Location TBD" : Location;

        public AppointmentItemViewModel(Appointment item)
        {
            Id = item.Id;
            PatientName = item.PatientName ?? string.Empty;
            Date = item.Date;
            Notes = item.Notes ?? string.Empty;
            DoctorId = item.DoctorId;
            DoctorName = item.DoctorName ?? string.Empty;
            Type = item.Type ?? string.Empty;
            Location = item.Location ?? string.Empty;
            Status = item.Status ?? string.Empty;
            StartTime = item.StartTime;
            EndTime = item.EndTime;
        }

        public Appointment ToAppointment() => new Appointment
        {
            Id = Id,
            PatientName = PatientName,
            DoctorId = DoctorId,
            DoctorName = DoctorName,
            Date = Date,
            StartTime = StartTime,
            EndTime = EndTime,
            Status = Status,
            Type = Type,
            Location = Location,
            Notes = Notes
        };
    }
}