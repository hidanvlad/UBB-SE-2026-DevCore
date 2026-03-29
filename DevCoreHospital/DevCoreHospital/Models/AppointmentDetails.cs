using System;

namespace DevCoreHospital.Models
{
    public sealed class AppointmentDetails
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime DateTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}