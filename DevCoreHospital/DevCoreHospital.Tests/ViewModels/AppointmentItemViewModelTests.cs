using System;
using DevCoreHospital.Models;
using DevCoreHospital.ViewModels.Doctor;

namespace DevCoreHospital.Tests.ViewModels
{
    public class AppointmentItemViewModelTests
    {
        private static Appointment BuildAppointment(
            int id = 1,
            string patientName = "Jane Doe",
            string doctorName = "Dr. Smith",
            string location = "Room 5",
            string status = "Scheduled",
            string type = "Checkup",
            string? notes = null,
            DateTime? date = null,
            TimeSpan? startTime = null,
            TimeSpan? endTime = null)
            => new Appointment
            {
                Id = id,
                PatientName = patientName,
                DoctorId = 10,
                DoctorName = doctorName,
                Location = location,
                Status = status,
                Type = type,
                Notes = notes ?? string.Empty,
                Date = date ?? new DateTime(2025, 6, 15),
                StartTime = startTime ?? new TimeSpan(9, 0, 0),
                EndTime = endTime ?? new TimeSpan(10, 0, 0),
            };

        [Fact]
        public void DateText_ReturnsFormattedDate()
        {
            var viewModel = new AppointmentItemViewModel(BuildAppointment(date: new DateTime(2025, 6, 15)));

            Assert.Equal("15 Jun 2025", viewModel.DateText);
        }

        [Fact]
        public void TimeRangeText_ContainsStartAndEndTime()
        {
            var viewModel = new AppointmentItemViewModel(BuildAppointment(
                startTime: new TimeSpan(9, 30, 0),
                endTime: new TimeSpan(10, 15, 0)));

            Assert.Contains("09:30", viewModel.TimeRangeText);
            Assert.Contains("10:15", viewModel.TimeRangeText);
        }

        [Fact]
        public void LocationSafe_ReturnsLocation_WhenLocationIsSet()
        {
            var viewModel = new AppointmentItemViewModel(BuildAppointment(location: "Room 5"));

            Assert.Equal("Room 5", viewModel.LocationSafe);
        }

        [Fact]
        public void LocationSafe_ReturnsLocationTbd_WhenLocationIsEmpty()
        {
            var viewModel = new AppointmentItemViewModel(BuildAppointment(location: string.Empty));

            Assert.Equal("Location TBD", viewModel.LocationSafe);
        }

        [Fact]
        public void LocationSafe_ReturnsLocationTbd_WhenLocationIsWhitespace()
        {
            var viewModel = new AppointmentItemViewModel(BuildAppointment(location: "   "));

            Assert.Equal("Location TBD", viewModel.LocationSafe);
        }

        [Fact]
        public void ToAppointment_ReturnsAppointmentWithSameId()
        {
            var viewModel = new AppointmentItemViewModel(BuildAppointment(id: 42));

            var result = viewModel.ToAppointment();

            Assert.Equal(42, result.Id);
        }

        [Fact]
        public void ToAppointment_PreservesAllFields()
        {
            var date = new DateTime(2025, 7, 10);
            var start = new TimeSpan(9, 0, 0);
            var end = new TimeSpan(11, 30, 0);
            var appt = BuildAppointment(
                id: 5, patientName: "Alice", doctorName: "Dr. Brown",
                location: "Ward B", status: "Scheduled", type: "Follow-up",
                notes: "Bring X-ray", date: date, startTime: start, endTime: end);
            var viewModel = new AppointmentItemViewModel(appt);

            var result = viewModel.ToAppointment();

            Assert.Equal(5, result.Id);
            Assert.Equal("Alice", result.PatientName);
            Assert.Equal("Dr. Brown", result.DoctorName);
            Assert.Equal("Ward B", result.Location);
            Assert.Equal("Scheduled", result.Status);
            Assert.Equal("Follow-up", result.Type);
            Assert.Equal("Bring X-ray", result.Notes);
            Assert.Equal(date, result.Date);
            Assert.Equal(start, result.StartTime);
            Assert.Equal(end, result.EndTime);
        }

        [Fact]
        public void Constructor_HandlesNullFields_WithEmptyStrings()
        {
            var appt = new Appointment
            {
                Id = 1,
                PatientName = null!,
                DoctorName = null!,
                Location = null!,
                Notes = null!,
                Type = null!,
                Status = null!,
                Date = DateTime.Now,
            };

            var viewModel = new AppointmentItemViewModel(appt);

            Assert.Equal(string.Empty, viewModel.PatientName);
            Assert.Equal(string.Empty, viewModel.DoctorName);
            Assert.Equal(string.Empty, viewModel.Notes);
        }
    }
}
