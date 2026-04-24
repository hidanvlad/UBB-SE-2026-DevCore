using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IAppointmentRepository
    {
        Task<IReadOnlyList<Appointment>> GetAppointmentsInRangeAsync(int doctorUserId, DateTime fromDate, DateTime toDate, int skip, int take);
        Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync();
        Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId);
        Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId);
        Task AddAppointmentAsync(int patientId, int doctorId, DateTime startTime, DateTime endTime, string status);
        Task UpdateAppointmentStatusAsync(int id, string status);
        Task<int> GetAppointmentsCountForDoctorByStatusAsync(int doctorId, string status);
        Task UpdateDoctorStatusAsync(int doctorId, string status);
    }
}
