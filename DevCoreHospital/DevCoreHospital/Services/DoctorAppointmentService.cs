using DevCoreHospital.Data;
using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public sealed class DoctorAppointmentService : IDoctorAppointmentService
    {
        private readonly IDoctorAppointmentDataSource _dataSource;

        public DoctorAppointmentService(IDoctorAppointmentDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        // Metodele de citire doar dau mai departe cererea către Repository
        public Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take) =>
            _dataSource.GetUpcomingAppointmentsAsync(doctorUserId, fromDate, skip, take);

        public Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync() =>
            _dataSource.GetAllDoctorsAsync();

        public Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId) =>
            _dataSource.GetAppointmentDetailsAsync(appointmentId);

        public Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId) =>
            _dataSource.GetAppointmentsForAdminAsync(doctorId);

        

        public async Task BookAppointmentAsync(Appointment appointment)
        {
            // Orice validare viitoare înainte de salvare o poți pune aici!
            await _dataSource.AddAppointmentAsync(appointment);
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            // 1. Salvăm statusul programării ca "Finished"
            await _dataSource.UpdateAppointmentStatusAsync(appointment.Id, "Finished");

            // 2. REGULA DE BUSINESS: Verificăm dacă doctorul mai are programări
            int activeAppointments = await _dataSource.GetActiveAppointmentsCountForDoctorAsync(appointment.DoctorId);

            // 3. Dacă e liber, îl facem AVAILABLE
            if (activeAppointments == 0)
            {
                await _dataSource.UpdateDoctorStatusAsync(appointment.DoctorId, "AVAILABLE");
            }
        }

        public async Task CancelAppointmentAsync(Appointment appointment)
        {
            // Anulăm programarea (Validarea cu "Cannot cancel finished" o faci în ViewModel / UI)
            await _dataSource.UpdateAppointmentStatusAsync(appointment.Id, "Canceled");
        }
    }
}