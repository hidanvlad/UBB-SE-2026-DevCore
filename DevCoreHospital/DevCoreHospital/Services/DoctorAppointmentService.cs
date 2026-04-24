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

        public async Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take)
        {
            DateTime from = fromDate.Date;
            DateTime to = from.AddDays(31);
            var appointments = await dataSource.GetAppointmentsInRangeAsync(doctorUserId, from, to, skip, take);
            return appointments.Select(ToDomainAppointment).ToList();
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            var doctors = await dataSource.GetAllDoctorsAsync();
            return doctors
                .Select(doctor => (DoctorId: doctor.DoctorId, DoctorName: (doctor.DoctorName ?? string.Empty).Trim()))
                .OrderBy(doctor => doctor.DoctorName)
                .ToList();
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            var appointment = await dataSource.GetAppointmentDetailsAsync(appointmentId);
            return appointment == null ? null : ToDomainAppointment(appointment);
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId)
        {
            var appointments = await dataSource.GetAppointmentsForAdminAsync(doctorId);
            return appointments.Select(ToDomainAppointment).ToList();
        }

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
            await PersistAppointmentAsync(appointment);
            await dataSource.UpdateDoctorStatusAsync(doctorId, "IN_EXAMINATION");
        }

        public async Task BookAppointmentAsync(Appointment appointment)
        {
            await PersistAppointmentAsync(appointment);
            await dataSource.UpdateDoctorStatusAsync(appointment.DoctorId, "IN_EXAMINATION");
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            if (string.Equals(appointment?.Status, "Finished", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This appointment is already finished.");
            }

            await dataSource.UpdateAppointmentStatusAsync(appointment!.Id, "Finished");
            appointment.Status = "Finished";

            int activeAppointments = await dataSource.GetAppointmentsCountForDoctorByStatusAsync(appointment.DoctorId, "Scheduled");

            if (activeAppointments == 0)
            {
                await dataSource.UpdateDoctorStatusAsync(appointment.DoctorId, "AVAILABLE");
            }
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsInRangeAsync(int doctorId, DateTime from, DateTime to)
        {
            const int maxAppointments = 500;
            var rawAppointments = await dataSource.GetAppointmentsInRangeAsync(doctorId, from, to, 0, maxAppointments);

            bool IsForDoctor(Appointment appointment) => appointment.DoctorId == doctorId;
            bool IsInRange(Appointment appointment)
            {
                var start = appointment.Date.Date + appointment.StartTime;
                var end = appointment.Date.Date + appointment.EndTime;
                if (end <= start)
                {
                    return false;
                }

                return start < to && end > from;
            }

            return rawAppointments
                .Select(ToDomainAppointment)
                .Where(IsForDoctor)
                .Where(IsInRange)
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
            appointment.Status = "Canceled";
        }

        public Task<IReadOnlyList<Shift>> GetShiftsForStaffInRangeAsync(int doctorId, DateTime from, DateTime to)
        {
            if (shiftRepository == null)
            {
                return Task.FromResult<IReadOnlyList<Shift>>(new List<Shift>());
            }

            bool IsNotCancelled(Shift shift) => shift.Status != ShiftStatus.CANCELLED;

            return Task.Run<IReadOnlyList<Shift>>(() =>
                shiftRepository
                    .GetShiftsForStaffInRange(doctorId, from, to)
                    .Where(IsNotCancelled)
                    .OrderBy(shift => shift.StartTime)
                    .ToList());
        }

        private async Task PersistAppointmentAsync(Appointment appointment)
        {
            int patientId = ParsePatientId(appointment.PatientName);
            DateTime start = appointment.Date.Date.Add(appointment.StartTime);
            DateTime end = appointment.Date.Date.Add(appointment.EndTime);
            string status = string.IsNullOrWhiteSpace(appointment.Status) ? "Scheduled" : appointment.Status;

            await dataSource.AddAppointmentAsync(patientId, appointment.DoctorId, start, end, status);
        }

        private static int ParsePatientId(string? patientName)
        {
            string rawPatientInput = patientName?.Replace("PAT-", string.Empty).Trim() ?? "0";
            int.TryParse(rawPatientInput, out int patientId);
            return patientId;
        }

        private static Appointment ToDomainAppointment(Appointment appointment)
        {
            string patientName;
            if (string.IsNullOrWhiteSpace(appointment.PatientName))
            {
                patientName = "PAT-0";
            }
            else if (appointment.PatientName.StartsWith("PAT-", StringComparison.OrdinalIgnoreCase))
            {
                patientName = appointment.PatientName;
            }
            else if (int.TryParse(appointment.PatientName, out var patientId))
            {
                patientName = "PAT-" + patientId;
            }
            else
            {
                patientName = appointment.PatientName;
            }

            string status = string.IsNullOrWhiteSpace(appointment.Status)
                ? "Scheduled"
                : appointment.Status;

            return new Appointment
            {
                Id = appointment.Id,
                DoctorId = appointment.DoctorId,
                DoctorName = (appointment.DoctorName ?? string.Empty).Trim(),
                PatientName = patientName,
                Date = appointment.Date,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                Status = status,
                Type = appointment.Type,
                Location = appointment.Location,
                Notes = appointment.Notes,
            };
        }
    }
}
