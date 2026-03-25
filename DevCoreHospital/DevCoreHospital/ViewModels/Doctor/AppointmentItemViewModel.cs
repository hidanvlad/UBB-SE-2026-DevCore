using System;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class AppointmentItemViewModel
    {
        public int Id { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string DateText => Date.ToString("dd MMM yyyy, HH:mm");
        public string Notes { get; set; } = string.Empty;

        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;

        // required by AppointmentDetailsPage
        public string Type { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public AppointmentItemViewModel(dynamic item)
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
        }
    }
}