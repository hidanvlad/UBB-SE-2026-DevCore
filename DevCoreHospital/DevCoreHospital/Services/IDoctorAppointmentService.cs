using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public interface IDoctorAppointmentService
    {
        Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorId, DateTime fromDate, int skip, int take);
        Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync(); // from Staff where role='Doctor'
        Task<AppointmentDetails> GetAppointmentDetailsAsync(int appointmentId);

        Task BookAppointmentAsync(Appointment appointment);
        Task FinishAppointmentAsync(Appointment appointment);
        Task CancelAppointmentAsync(Appointment appointment);
    }
}