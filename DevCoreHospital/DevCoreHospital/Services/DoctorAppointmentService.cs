using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using DevCoreHospital.Data;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public sealed class DoctorAppointmentService : IDoctorAppointmentService
    {
        private readonly IDoctorAppointmentDataSource dataSource;

        public DoctorAppointmentService(IDoctorAppointmentDataSource dataSource)
        {
            this.dataSource = dataSource;
        }

        public Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take) =>
            dataSource.GetUpcomingAppointmentsAsync(doctorUserId, fromDate, skip, take);

        public Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync() =>
            dataSource.GetAllDoctorsAsync();

        public Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId) =>
            dataSource.GetAppointmentDetailsAsync(appointmentId);

        public Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId) =>
            dataSource.GetAppointmentsForAdminAsync(doctorId);

        public async Task BookAppointmentAsync(Appointment appointment)
        {
            await dataSource.AddAppointmentAsync(appointment);
            await dataSource.UpdateDoctorStatusAsync(appointment.DoctorId, "IN_EXAMINATION");
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            await dataSource.UpdateAppointmentStatusAsync(appointment.Id, "Finished");

            int activeAppointments = await dataSource.GetActiveAppointmentsCountForDoctorAsync(appointment.DoctorId);

            if (activeAppointments == 0)
            {
                await dataSource.UpdateDoctorStatusAsync(appointment.DoctorId, "AVAILABLE");
            }
        }

        public async Task CancelAppointmentAsync(Appointment appointment)
        {
            if (string.Equals(appointment?.Status, "Finished", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Cannot cancel an appointment that is already Finished.");
            }

            await dataSource.UpdateAppointmentStatusAsync(appointment!.Id, "Canceled");
        }
    }
}