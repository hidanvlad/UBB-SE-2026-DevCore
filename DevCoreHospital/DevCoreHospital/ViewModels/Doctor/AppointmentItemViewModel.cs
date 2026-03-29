using System;
using DevCoreHospital.Models;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class AppointmentItemViewModel
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime DateTime { get; set; }
        public string Status { get; set; } = string.Empty;

        public string DateText => DateTime.ToString("dd MMM yyyy");
        public string TimeText => DateTime.ToString("HH:mm");

        public AppointmentItemViewModel(Appointment item)
        {
            Id = item.Id;
            PatientId = item.PatientId;
            DoctorId = item.DoctorId;
            DateTime = item.DateTime;
            Status = item.Status ?? string.Empty;
        }
    }
}