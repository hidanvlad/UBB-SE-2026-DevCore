using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IDoctorAppointmentService
    {
        Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skipCount, int takeCount);
        Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync();
        Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId);
        Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId);
        Task CreateAppointmentAsync(string patientName, int doctorId, DateTime date, TimeSpan startTime);
        Task BookAppointmentAsync(Appointment appointment);
        Task FinishAppointmentAsync(Appointment appointment);
        Task<IReadOnlyList<Appointment>> GetAppointmentsInRangeAsync(int doctorId, DateTime fromDate, DateTime toDate);
        Task CancelAppointmentAsync(Appointment appointment);

        Task<IReadOnlyList<Shift>> GetShiftsForStaffInRangeAsync(int doctorId, DateTime fromDate, DateTime toDate);
    }
}