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
        private const int UpcomingAppointmentsWindowDays = 31;
        private const int DefaultAppointmentDurationMinutes = 30;
        private const int DefaultPatientId = 0;
        private const int NoActiveAppointmentsCount = 0;
        private const string ScheduledStatus = "Scheduled";
        private const string FinishedStatus = "Finished";
        private const string CanceledStatus = "Canceled";
        private const string InExaminationStatus = "IN_EXAMINATION";
        private const string AvailableStatus = "AVAILABLE";
        private const string PatientNamePrefix = "PAT-";
        private const string DefaultPatientIdString = "0";

        private readonly IAppointmentRepository dataSource;
        private readonly IStaffRepository staffRepository;
        private readonly IShiftRepository? shiftRepository;

        public DoctorAppointmentService(IAppointmentRepository dataSource, IStaffRepository staffRepository, IShiftRepository? shiftRepository = null)
        {
            this.dataSource = dataSource;
            this.staffRepository = staffRepository;
            this.shiftRepository = shiftRepository;
        }

        public async Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skipCount, int takeCount)
        {
            DateTime from = fromDate.Date;
            DateTime to = from.AddDays(UpcomingAppointmentsWindowDays);
            var allAppointments = await dataSource.GetAllAppointmentsAsync();

            bool IsForDoctor(Appointment appointment) => appointment.DoctorId == doctorUserId;
            bool IsWithinWindow(Appointment appointment)
            {
                DateTime appointmentStart = appointment.Date.Add(appointment.StartTime);
                return appointmentStart >= from && appointmentStart < to;
            }

            DateTime ByDate(Appointment appointment) => appointment.Date;
            TimeSpan ByStartTime(Appointment appointment) => appointment.StartTime;

            return allAppointments
                .Where(IsForDoctor)
                .Where(IsWithinWindow)
                .OrderBy(ByDate)
                .ThenBy(ByStartTime)
                .Skip(skipCount)
                .Take(takeCount)
                .Select(ToDomainAppointment)
                .ToList();
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            var doctors = await staffRepository.GetAllDoctorsAsync();

            (int DoctorId, string DoctorName) ToDoctorOption((int DoctorId, string FirstName, string LastName) doctor) =>
                (doctor.DoctorId, ((doctor.FirstName ?? string.Empty) + " " + (doctor.LastName ?? string.Empty)).Trim());

            string ByDoctorName((int DoctorId, string DoctorName) doctor) => doctor.DoctorName;

            return doctors
                .Select(ToDoctorOption)
                .OrderBy(ByDoctorName)
                .ToList();
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            var allAppointments = await dataSource.GetAllAppointmentsAsync();
            bool HasMatchingId(Appointment existingAppointment) => existingAppointment.Id == appointmentId;

            var appointment = allAppointments.FirstOrDefault(HasMatchingId);
            return appointment == null ? null : ToDomainAppointment(appointment);
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId)
        {
            var allAppointments = await dataSource.GetAllAppointmentsAsync();

            bool IsForDoctor(Appointment appointment) => appointment.DoctorId == doctorId;
            DateTime ByDate(Appointment appointment) => appointment.Date;
            TimeSpan ByStartTime(Appointment appointment) => appointment.StartTime;

            return allAppointments
                .Where(IsForDoctor)
                .OrderBy(ByDate)
                .ThenBy(ByStartTime)
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
                EndTime = startTime.Add(TimeSpan.FromMinutes(DefaultAppointmentDurationMinutes)),
                Status = ScheduledStatus,
            };
            await PersistAppointmentAsync(appointment);
            await staffRepository.UpdateStatusAsync(doctorId, InExaminationStatus);
        }

        public async Task BookAppointmentAsync(Appointment appointment)
        {
            await PersistAppointmentAsync(appointment);
            await staffRepository.UpdateStatusAsync(appointment.DoctorId, InExaminationStatus);
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            if (string.Equals(appointment?.Status, FinishedStatus, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This appointment is already finished.");
            }

            await dataSource.UpdateAppointmentStatusAsync(appointment!.Id, FinishedStatus);
            appointment.Status = FinishedStatus;

            var allAppointments = await dataSource.GetAllAppointmentsAsync();

            bool IsScheduledForSameDoctor(Appointment existingAppointment) =>
                existingAppointment.DoctorId == appointment.DoctorId
                && string.Equals(existingAppointment.Status, ScheduledStatus, StringComparison.OrdinalIgnoreCase);

            int activeAppointments = allAppointments.Count(IsScheduledForSameDoctor);

            if (activeAppointments == NoActiveAppointmentsCount)
            {
                await staffRepository.UpdateStatusAsync(appointment.DoctorId, AvailableStatus);
            }
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsInRangeAsync(int doctorId, DateTime fromDate, DateTime toDate)
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

                return start < toDate && end > fromDate;
            }

            DateTime ByDate(Appointment appointment) => appointment.Date;
            TimeSpan ByStartTime(Appointment appointment) => appointment.StartTime;

            return rawAppointments
                .Select(ToDomainAppointment)
                .Where(IsForDoctor)
                .Where(IsInRange)
                .OrderBy(ByDate)
                .ThenBy(ByStartTime)
                .ToList();
        }

        public async Task CancelAppointmentAsync(Appointment appointment)
        {
            if (string.Equals(appointment?.Status, FinishedStatus, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot cancel an appointment that is already Finished.");
            }

            await dataSource.UpdateAppointmentStatusAsync(appointment!.Id, CanceledStatus);
            appointment.Status = CanceledStatus;
        }

        public Task<IReadOnlyList<Shift>> GetShiftsForStaffInRangeAsync(int doctorId, DateTime fromDate, DateTime toDate)
        {
            if (shiftRepository == null)
            {
                return Task.FromResult<IReadOnlyList<Shift>>(new List<Shift>());
            }

            bool IsForDoctorInRange(Shift shift) =>
                shift.AppointedStaff.StaffID == doctorId
                && shift.StartTime < toDate
                && shift.EndTime > fromDate
                && shift.Status != ShiftStatus.CANCELLED;

            DateTime ByStartTime(Shift shift) => shift.StartTime;

            IReadOnlyList<Shift> LoadAndFilter() => shiftRepository
                .GetAllShifts()
                .Where(IsForDoctorInRange)
                .OrderBy(ByStartTime)
                .ToList();

            return Task.Run(LoadAndFilter);
        }

        private async Task PersistAppointmentAsync(Appointment appointment)
        {
            int patientId = ParsePatientId(appointment.PatientName);
            DateTime start = appointment.Date.Date.Add(appointment.StartTime);
            DateTime end = appointment.Date.Date.Add(appointment.EndTime);
            string status = string.IsNullOrWhiteSpace(appointment.Status) ? ScheduledStatus : appointment.Status;

            await dataSource.AddAppointmentAsync(patientId, appointment.DoctorId, start, end, status);
        }

        private static int ParsePatientId(string? patientName)
        {
            string rawPatientInput = patientName?.Replace(PatientNamePrefix, string.Empty).Trim() ?? DefaultPatientIdString;
            return int.TryParse(rawPatientInput, out int patientId) ? patientId : DefaultPatientId;
        }

        private static Appointment ToDomainAppointment(Appointment appointment)
        {
            const string DefaultPatientName = PatientNamePrefix + DefaultPatientIdString;

            string patientName;
            if (string.IsNullOrWhiteSpace(appointment.PatientName))
            {
                patientName = DefaultPatientName;
            }
            else if (appointment.PatientName.StartsWith(PatientNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                patientName = appointment.PatientName;
            }
            else if (int.TryParse(appointment.PatientName, out var patientId))
            {
                patientName = PatientNamePrefix + patientId;
            }
            else
            {
                patientName = appointment.PatientName;
            }

            string status = string.IsNullOrWhiteSpace(appointment.Status)
                ? ScheduledStatus
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
