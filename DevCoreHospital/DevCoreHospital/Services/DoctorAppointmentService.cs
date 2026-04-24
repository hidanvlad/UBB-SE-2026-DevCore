using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public sealed class DoctorAppointmentService : IDoctorAppointmentService
    {
        private readonly IAppointmentRepository dataSource;
        private readonly IShiftRepository? shiftRepository;

        public DoctorAppointmentService(IAppointmentRepository dataSource, IShiftRepository? shiftRepository = null)
        {
            this.dataSource = dataSource;
            this.shiftRepository = shiftRepository;
        }

        public Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take) =>
            dataSource.GetUpcomingAppointmentsAsync(doctorUserId, fromDate, skip, take);

        public Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync() =>
            dataSource.GetAllDoctorsAsync();

        public Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId) =>
            dataSource.GetAppointmentDetailsAsync(appointmentId);

        public Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId) =>
            dataSource.GetAppointmentsForAdminAsync(doctorId);

        public async Task CreateAppointmentAsync(string patientName, int doctorId, DateTime date, TimeSpan startTime)
        {
            var appointment = new Appointment
            {
                PatientName = patientName,
                DoctorId = doctorId,
                Date = date.Date,
                StartTime = startTime,
                EndTime = startTime.Add(TimeSpan.FromMinutes(30)),
                Status = "Scheduled",
            };
            await dataSource.AddAppointmentAsync(appointment);
            await dataSource.UpdateDoctorStatusAsync(doctorId, "IN_EXAMINATION");
        }

        public async Task BookAppointmentAsync(Appointment appointment)
        {
            await dataSource.AddAppointmentAsync(appointment);
            await dataSource.UpdateDoctorStatusAsync(appointment.DoctorId, "IN_EXAMINATION");
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            if (string.Equals(appointment?.Status, "Finished", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This appointment is already finished.");
            }

            await dataSource.UpdateAppointmentStatusAsync(appointment!.Id, "Finished");

            int activeAppointments = await dataSource.GetActiveAppointmentsCountForDoctorAsync(appointment.DoctorId);

            if (activeAppointments == 0)
            {
                await dataSource.UpdateDoctorStatusAsync(appointment.DoctorId, "AVAILABLE");
            }
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsInRangeAsync(int doctorId, DateTime from, DateTime to)
        {
            const int maxAppointments = 500;
            var rawAppointments = await dataSource.GetUpcomingAppointmentsAsync(doctorId, from, 0, maxAppointments);
            return rawAppointments
                .Where(appointment => appointment.DoctorId == doctorId)
                .Where(appointment =>
                {
                    var start = appointment.Date.Date + appointment.StartTime;
                    var end = appointment.Date.Date + appointment.EndTime;
                    if (end <= start)
                    {
                        return false;
                    }

                    return start < to && end > from;
                })
                .OrderBy(appointment => appointment.Date)
                .ThenBy(appointment => appointment.StartTime)
                .ToList();
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

        public Task<IReadOnlyList<Shift>> GetShiftsForStaffInRangeAsync(int doctorId, DateTime from, DateTime to)
        {
            if (shiftRepository == null)
            {
                return Task.FromResult<IReadOnlyList<Shift>>(new List<Shift>());
            }

            return Task.Run<IReadOnlyList<Shift>>(() =>
                shiftRepository
                    .GetShiftsForStaffInRange(doctorId, from, to)
                    .Where(shift => shift.Status != ShiftStatus.CANCELLED)
                    .OrderBy(shift => shift.StartTime)
                    .ToList());
        }
    }
}