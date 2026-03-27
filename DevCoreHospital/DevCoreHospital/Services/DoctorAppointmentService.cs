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

        public Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorId, DateTime fromDate, int skip, int take) =>
            _repository.GetUpcomingAppointmentsAsync(doctorId, fromDate, skip, take);

        // IMPORTANT: Doctors come from Staff table (role='Doctor')
        public Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync() =>
            _repository.GetAllDoctorsAsync();

        public Task<AppointmentDetails> GetAppointmentDetailsAsync(int appointmentId) =>
            _repository.GetAppointmentDetailsAsync(appointmentId);

        public async Task BookAppointmentAsync(Appointment appointment)
        {
            await _repository.AddAppointmentAsync(appointment);
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            await _repository.UpdateAppointmentStatusAsync(appointment.Id, "Finished");

            int activeAppointments = await _repository.GetActiveAppointmentsCountForDoctorAsync(appointment.DoctorId);
            if (activeAppointments == 0)
            {
                await _repository.UpdateDoctorAvailabilityAsync(appointment.DoctorId, true);
                await _repository.UpdateDoctorStatusAsync(appointment.DoctorId, "AVAILABLE");
            }
        }

        public async Task CancelAppointmentAsync(Appointment appointment)
        {
            await _repository.UpdateAppointmentStatusAsync(appointment.Id, "Canceled");
        }
    }
}