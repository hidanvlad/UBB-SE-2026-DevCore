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
        private readonly IStaffRepository staffRepository;
        private readonly IShiftRepository? shiftRepository;

        public DoctorAppointmentService(IAppointmentRepository dataSource, IStaffRepository staffRepository, IShiftRepository? shiftRepository = null)
        {
            this.dataSource = dataSource;
            this.staffRepository = staffRepository;
            this.shiftRepository = shiftRepository;
        }

        public async Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take)
        {
            DateTime from = fromDate.Date;
            DateTime to = from.AddDays(31);
            var all = await dataSource.GetAllAppointmentsAsync();
            return all
                .Where(appointment => appointment.DoctorId == doctorUserId)
                .Where(appointment => appointment.Date.Add(appointment.StartTime) >= from
                    && appointment.Date.Add(appointment.StartTime) < to)
                .OrderBy(appointment => appointment.Date)
                .ThenBy(appointment => appointment.StartTime)
                .Skip(skip)
                .Take(take)
                .Select(ToDomainAppointment)
                .ToList();
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            var doctors = await staffRepository.GetAllDoctorsAsync();
            return doctors
                .Select(doctor => (
                    DoctorId: doctor.DoctorId,
                    DoctorName: ((doctor.FirstName ?? string.Empty) + " " + (doctor.LastName ?? string.Empty)).Trim()))
                .OrderBy(doctor => doctor.DoctorName)
                .ToList();
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            var all = await dataSource.GetAllAppointmentsAsync();
            var appointment = all.FirstOrDefault(a => a.Id == appointmentId);
            return appointment == null ? null : ToDomainAppointment(appointment);
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId)
        {
            var all = await dataSource.GetAllAppointmentsAsync();
            return all
                .Where(appointment => appointment.DoctorId == doctorId)
                .OrderBy(appointment => appointment.Date)
                .ThenBy(appointment => appointment.StartTime)
                .Select(ToDomainAppointment)
                .ToList();
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
            await staffRepository.UpdateStatusAsync(doctorId, "IN_EXAMINATION");
        }

        public async Task BookAppointmentAsync(Appointment appointment)
        {
            await PersistAppointmentAsync(appointment);
            await staffRepository.UpdateStatusAsync(appointment.DoctorId, "IN_EXAMINATION");
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            if (string.Equals(appointment?.Status, "Finished", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This appointment is already finished.");
            }

            await dataSource.UpdateAppointmentStatusAsync(appointment!.Id, "Finished");
            appointment.Status = "Finished";

            var all = await dataSource.GetAllAppointmentsAsync();
            int activeAppointments = all.Count(a =>
                a.DoctorId == appointment.DoctorId
                && string.Equals(a.Status, "Scheduled", StringComparison.OrdinalIgnoreCase));

            if (activeAppointments == 0)
            {
                await staffRepository.UpdateStatusAsync(appointment.DoctorId, "AVAILABLE");
            }
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsInRangeAsync(int doctorId, DateTime from, DateTime to)
        {
            var rawAppointments = await dataSource.GetAllAppointmentsAsync();

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

            bool IsForDoctorInRange(Shift shift) =>
                shift.AppointedStaff.StaffID == doctorId
                && shift.StartTime < to
                && shift.EndTime > from
                && shift.Status != ShiftStatus.CANCELLED;

            return Task.Run<IReadOnlyList<Shift>>(() =>
                shiftRepository
                    .GetAllShifts()
                    .Where(IsForDoctorInRange)
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
