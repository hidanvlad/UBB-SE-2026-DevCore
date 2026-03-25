using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public interface IDoctorAppointmentService
    {
        Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take);
        Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync();

        // NEW:
        Task<AppointmentDetails?> GetAppointmentDetailsAsync(int appointmentId);
    }
}