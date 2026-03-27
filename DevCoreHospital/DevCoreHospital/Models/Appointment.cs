using System;

namespace DevCoreHospital.Models
{
    public class Appointment
    {
        public int Id { get; set; }              // appointment_id
        public int PatientId { get; set; }       // patient_id
        public int DoctorId { get; set; }        // doctor_id -> Staff.staff_id
        public DateTime DateTime { get; set; }   // date_time
        public string Status { get; set; } = string.Empty;
    }
}