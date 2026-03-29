using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevCoreHospital.Data
{
    public sealed class MockDoctorAppointmentDataSource : IDoctorAppointmentDataSource
    {
        private readonly List<(int DoctorId, string DoctorName)> _doctors;
        private readonly List<Appointment> _appointments;
        private int _nextAppointmentId;

        public MockDoctorAppointmentDataSource()
        {
            _doctors = new List<(int DoctorId, string DoctorName)>
            {
                (1, "Dr. Mihai Pop"),
                (2, "Dr. Ana Ionescu"),
                (3, "Dr. Raul Petrescu"),
                (4, "Dr. Teodora Rusu")
            };

            var today = DateTime.Today;
            _appointments = new List<Appointment>
            {
                NewAppointment(1001, 1, "PAT-12001", today, new TimeSpan(9, 0, 0), new TimeSpan(9, 30, 0), "Scheduled", "Consult", "Room A"),
                NewAppointment(1002, 1, "PAT-12002", today, new TimeSpan(11, 0, 0), new TimeSpan(11, 30, 0), "Scheduled", "Control", "Room B"),
                NewAppointment(1003, 2, "PAT-12003", today.AddDays(1), new TimeSpan(10, 0, 0), new TimeSpan(10, 45, 0), "Scheduled", "Cardiology", "Room C"),
                NewAppointment(1004, 3, "PAT-12004", today.AddDays(2), new TimeSpan(14, 0, 0), new TimeSpan(14, 30, 0), "Scheduled", "ER follow-up", "ER 2"),
                NewAppointment(1005, 4, "PAT-12005", today.AddDays(3), new TimeSpan(8, 30, 0), new TimeSpan(9, 0, 0), "Scheduled", "Neurology", "Room D")
            };

            _nextAppointmentId = _appointments.Max(a => a.Id) + 1;
        }

        public Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take)
        {
            var upperBound = fromDate.Date.AddDays(8);
            var result = _appointments
                .Where(a => a.DoctorId == doctorUserId)
                .Where(a => a.Date.Date >= fromDate.Date && a.Date.Date < upperBound)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.StartTime)
                .Skip(skip)
                .Take(take)
                .Select(Clone)
                .ToList();

            return Task.FromResult<IReadOnlyList<Appointment>>(result);
        }

        public Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            return Task.FromResult<IReadOnlyList<(int DoctorId, string DoctorName)>>(_doctors.ToList());
        }

        public Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            var match = _appointments.FirstOrDefault(a => a.Id == appointmentId);
            return Task.FromResult(match is null ? null : Clone(match));
        }

        public Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId)
        {
            var result = _appointments
                .Where(a => a.DoctorId == doctorId)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.StartTime)
                .Select(Clone)
                .ToList();

            return Task.FromResult<IReadOnlyList<Appointment>>(result);
        }

        public Task AddAppointmentAsync(Appointment appt)
        {
            var copy = Clone(appt);
            copy.Id = _nextAppointmentId++;
            copy.Status = string.IsNullOrWhiteSpace(copy.Status) ? "Scheduled" : copy.Status;
            copy.DoctorName = ResolveDoctorName(copy.DoctorId);
            _appointments.Add(copy);
            return Task.CompletedTask;
        }

        public Task UpdateAppointmentStatusAsync(int id, string status)
        {
            var match = _appointments.FirstOrDefault(a => a.Id == id);
            if (match != null)
                match.Status = status;

            return Task.CompletedTask;
        }

        public Task<int> GetActiveAppointmentsCountForDoctorAsync(int doctorId)
        {
            var count = _appointments.Count(a =>
                a.DoctorId == doctorId &&
                string.Equals(a.Status, "Scheduled", StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(count);
        }

        public Task UpdateDoctorStatusAsync(int doctorId, string status)
        {
            return Task.CompletedTask;
        }

        private Appointment NewAppointment(int id, int doctorId, string patientName, DateTime date, TimeSpan start, TimeSpan end, string status, string type, string location)
        {
            return new Appointment
            {
                Id = id,
                DoctorId = doctorId,
                DoctorName = ResolveDoctorName(doctorId),
                PatientName = patientName,
                Date = date.Date,
                StartTime = start,
                EndTime = end,
                Status = status,
                Type = type,
                Location = location
            };
        }

        private string ResolveDoctorName(int doctorId)
        {
            return _doctors.FirstOrDefault(d => d.DoctorId == doctorId).DoctorName ?? "Unknown Doctor";
        }

        private static Appointment Clone(Appointment source)
        {
            return new Appointment
            {
                Id = source.Id,
                PatientName = source.PatientName,
                DoctorId = source.DoctorId,
                DoctorName = source.DoctorName,
                Date = source.Date,
                StartTime = source.StartTime,
                EndTime = source.EndTime,
                Status = source.Status,
                Type = source.Type,
                Location = source.Location,
                Notes = source.Notes
            };
        }
    }
}

