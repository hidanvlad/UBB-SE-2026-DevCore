using System;

namespace DevCoreHospital.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public string PatientName { get; set; } = string.Empty;

        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        // FIX: these must be TimeSpan (DB TIME columns)
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;
    }
}