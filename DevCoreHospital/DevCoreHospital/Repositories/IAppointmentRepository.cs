using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IAppointmentRepository
    {
        Task<IReadOnlyList<Appointment>> GetAllAppointmentsAsync();
        Task AddAppointmentAsync(int patientId, int doctorId, DateTime startTime, DateTime endTime, string status);
        Task UpdateAppointmentStatusAsync(int id, string status);
    }
}
