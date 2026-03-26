using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevCoreHospital.Services
{
    public sealed class DoctorAppointmentService : IDoctorAppointmentService
    {
        private readonly AppointmentRepository _repository;

        public DoctorAppointmentService(AppointmentRepository repository)
        {
            _repository = repository;
        }

        // Metodele de citire doar dau mai departe cererea către Repository
        public Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take) =>
            _repository.GetUpcomingAppointmentsAsync(doctorUserId, fromDate, skip, take);

        public Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync() =>
            _repository.GetAllDoctorsAsync();

        public Task<Appointment> GetAppointmentDetailsAsync(int appointmentId) =>
            _repository.GetAppointmentDetailsAsync(appointmentId);

        public Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId) =>
            _repository.GetAppointmentsForAdminAsync(doctorId);

        // ==========================================
        // REGULILE TALE DE BUSINESS
        // ==========================================

        public async Task BookAppointmentAsync(Appointment appointment)
        {
            // Orice validare viitoare înainte de salvare o poți pune aici!
            await _repository.AddAppointmentAsync(appointment);
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            // 1. Salvăm statusul programării ca "Finished"
            await _repository.UpdateAppointmentStatusAsync(appointment.Id, "Finished");

            // 2. REGULA DE BUSINESS: Verificăm dacă doctorul mai are programări
            int activeAppointments = await _repository.GetActiveAppointmentsCountForDoctorAsync(appointment.DoctorId);

            // 3. Dacă e liber, îl facem AVAILABLE
            if (activeAppointments == 0)
            {
                await _repository.UpdateDoctorStatusAsync(appointment.DoctorId, "AVAILABLE");
            }
        }

        public async Task CancelAppointmentAsync(Appointment appointment)
        {
            // Anulăm programarea (Validarea cu "Cannot cancel finished" o faci în ViewModel / UI)
            await _repository.UpdateAppointmentStatusAsync(appointment.Id, "Canceled");
        }
    }
}