using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class DoctorAppointmentServiceTests
    {
        private readonly Mock<IAppointmentRepository> mockDataSource;
        private readonly Mock<IStaffRepository> mockStaffRepository;
        private readonly Mock<IShiftRepository> mockShiftRepository;
        private readonly DoctorAppointmentService service;

        public DoctorAppointmentServiceTests()
        {
            mockDataSource = new Mock<IAppointmentRepository>();
            mockStaffRepository = new Mock<IStaffRepository>();
            mockShiftRepository = new Mock<IShiftRepository>();
            service = new DoctorAppointmentService(mockDataSource.Object, mockStaffRepository.Object, mockShiftRepository.Object);
        }


        [Fact]
        public async Task BookAppointmentAsync_AddsAppointment_WithGivenAppointmentObject()
        {
            var appointment = new Appointment
            {
                Id = 1,
                DoctorId = 10,
                PatientName = "PAT-42",
                Date = new DateTime(2025, 8, 1),
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(9, 30, 0),
                Status = "Scheduled",
            };

            await service.BookAppointmentAsync(appointment);

            mockDataSource.Verify(appointmentRepository => appointmentRepository.AddAppointmentAsync(
                42,
                10,
                new DateTime(2025, 8, 1, 9, 0, 0),
                new DateTime(2025, 8, 1, 9, 30, 0),
                "Scheduled"), Times.Once);
        }

        [Fact]
        public async Task BookAppointmentAsync_SetsDoctorStatus_ToInExamination()
        {
            var appointment = new Appointment { Id = 1, DoctorId = 10 };

            await service.BookAppointmentAsync(appointment);

            mockStaffRepository.Verify(staffRepository => staffRepository.UpdateStatusAsync(10, "IN_EXAMINATION"), Times.Once);
        }


        [Fact]
        public async Task FinishAppointmentAsync_SetsAppointmentStatus_ToFinished()
        {
            var appointment = new Appointment { Id = 5, DoctorId = 10 };
            mockDataSource.Setup(appointmentRepository => appointmentRepository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment> { new Appointment { Id = 6, DoctorId = 10, Status = "Scheduled" } });

            await service.FinishAppointmentAsync(appointment);

            mockDataSource.Verify(appointmentRepository => appointmentRepository.UpdateAppointmentStatusAsync(5, "Finished"), Times.Once);
        }

        [Fact]
        public async Task FinishAppointmentAsync_SetsDoctorStatus_ToAvailable_WhenNoActiveAppointmentsRemain()
        {
            var appointment = new Appointment { Id = 5, DoctorId = 10 };
            mockDataSource.Setup(appointmentRepository => appointmentRepository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment>());

            await service.FinishAppointmentAsync(appointment);

            mockStaffRepository.Verify(staffRepository => staffRepository.UpdateStatusAsync(10, "AVAILABLE"), Times.Once);
        }

        [Fact]
        public async Task FinishAppointmentAsync_DoesNotUpdateDoctorStatus_WhenActiveAppointmentsRemain()
        {
            var appointment = new Appointment { Id = 5, DoctorId = 10 };
            mockDataSource.Setup(appointmentRepository => appointmentRepository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment>
                {
                    new Appointment { Id = 6, DoctorId = 10, Status = "Scheduled" },
                    new Appointment { Id = 7, DoctorId = 10, Status = "Scheduled" },
                });

            await service.FinishAppointmentAsync(appointment);

            mockStaffRepository.Verify(staffRepository => staffRepository.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }


        [Fact]
        public async Task CancelAppointmentAsync_ThrowsInvalidOperationException_WhenAppointmentIsAlreadyFinished()
        {
            var finishedAppointment = new Appointment { Id = 3, DoctorId = 10, Status = "Finished" };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CancelAppointmentAsync(finishedAppointment));

            Assert.Equal("Cannot cancel an appointment that is already Finished.", exception.Message);
        }

        [Fact]
        public async Task CancelAppointmentAsync_WhenStatusIsFinishedWithDifferentCase_ThrowsInvalidOperationException()
        {
            var finishedAppointment = new Appointment { Id = 3, DoctorId = 10, Status = "FINISHED" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CancelAppointmentAsync(finishedAppointment));
        }

        [Fact]
        public async Task CancelAppointmentAsync_SetsAppointmentStatus_ToCanceled_WhenAppointmentIsScheduled()
        {
            var scheduledAppointment = new Appointment { Id = 3, DoctorId = 10, Status = "Scheduled" };

            await service.CancelAppointmentAsync(scheduledAppointment);

            mockDataSource.Verify(appointmentRepository => appointmentRepository.UpdateAppointmentStatusAsync(3, "Canceled"), Times.Once);
        }

        [Fact]
        public async Task CancelAppointmentAsync_DoesNotUpdateStatus_WhenAppointmentIsAlreadyFinished()
        {
            var finishedAppointment = new Appointment { Id = 3, DoctorId = 10, Status = "Finished" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CancelAppointmentAsync(finishedAppointment));

            mockDataSource.Verify(appointmentRepository => appointmentRepository.UpdateAppointmentStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetAllDoctorsAsync_WhenNamesUnordered_ReturnsThemAlphabeticallySorted()
        {
            IReadOnlyList<(int DoctorId, string FirstName, string LastName)> raw = new List<(int, string, string)>
            {
                (1, "Dr.", "Smith"),
                (2, "Dr.", "Jones"),
            };
            mockStaffRepository.Setup(staffRepository => staffRepository.GetAllDoctorsAsync()).ReturnsAsync(raw);

            var allDoctors = await service.GetAllDoctorsAsync();

            Assert.Equal(new[] { (2, "Dr. Jones"), (1, "Dr. Smith") }, allDoctors);
        }

        [Fact]
        public async Task GetAppointmentDetailsAsync_ReturnsMatchingAppointment()
        {
            var stored = new Appointment { Id = 42, DoctorId = 5, PatientName = "Jane Doe" };
            mockDataSource.Setup(appointmentRepository => appointmentRepository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment> { stored });

            var appointmentDetails = await service.GetAppointmentDetailsAsync(42);

            Assert.NotNull(appointmentDetails);
            Assert.Equal(42, appointmentDetails!.Id);
            Assert.Equal(5, appointmentDetails.DoctorId);
        }

        [Fact]
        public async Task GetAppointmentDetailsAsync_ReturnsNull_WhenNotFound()
        {
            mockDataSource.Setup(appointmentRepository => appointmentRepository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment>());

            var appointmentDetails = await service.GetAppointmentDetailsAsync(99);

            Assert.Null(appointmentDetails);
        }


        [Fact]
        public async Task CreateAppointmentAsync_AddsAppointment_WithScheduledStatusAndThirtyMinuteDuration()
        {
            var date = new DateTime(2025, 8, 1);
            var startTime = new TimeSpan(10, 0, 0);

            await service.CreateAppointmentAsync("PAT-1", 5, date, startTime);

            mockDataSource.Verify(appointmentRepository => appointmentRepository.AddAppointmentAsync(
                1,
                5,
                new DateTime(2025, 8, 1, 10, 0, 0),
                new DateTime(2025, 8, 1, 10, 30, 0),
                "Scheduled"), Times.Once);
        }

        [Fact]
        public async Task CreateAppointmentAsync_SetsDoctorStatus_ToInExamination()
        {
            await service.CreateAppointmentAsync("PAT-1", 7, DateTime.Today, TimeSpan.Zero);

            mockStaffRepository.Verify(staffRepository => staffRepository.UpdateStatusAsync(7, "IN_EXAMINATION"), Times.Once);
        }


        [Fact]
        public async Task FinishAppointmentAsync_ThrowsInvalidOperationException_WhenAppointmentIsAlreadyFinished()
        {
            var finishedAppointment = new Appointment { Id = 5, DoctorId = 10, Status = "Finished" };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.FinishAppointmentAsync(finishedAppointment));

            Assert.Equal("This appointment is already finished.", exception.Message);
        }

        [Fact]
        public async Task FinishAppointmentAsync_WhenStatusIsFinishedWithDifferentCase_ThrowsInvalidOperationException()
        {
            var finishedAppointment = new Appointment { Id = 5, DoctorId = 10, Status = "FINISHED" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.FinishAppointmentAsync(finishedAppointment));
        }

        [Fact]
        public async Task FinishAppointmentAsync_DoesNotUpdateStatus_WhenAppointmentIsAlreadyFinished()
        {
            var finishedAppointment = new Appointment { Id = 5, DoctorId = 10, Status = "Finished" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.FinishAppointmentAsync(finishedAppointment));

            mockDataSource.Verify(appointmentRepository => appointmentRepository.UpdateAppointmentStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }


        [Fact]
        public async Task GetAppointmentsInRangeAsync_ExcludesAppointment_WhenEndTimeEqualsStartTime()
        {
            var from = new DateTime(2025, 6, 11);
            var to = from.AddDays(1);
            var appointment = new Appointment
            {
                DoctorId = 1,
                Date = from,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
            };
            mockDataSource
                .Setup(appointmentRepository => appointmentRepository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment> { appointment });

            var appointments = await service.GetAppointmentsInRangeAsync(1, from, to);

            Assert.Empty(appointments);
        }

        [Fact]
        public async Task GetAppointmentsInRangeAsync_ExcludesAppointment_WhenEndTimeIsBeforeStartTime()
        {
            var from = new DateTime(2025, 6, 11);
            var to = from.AddDays(1);
            var appointment = new Appointment
            {
                DoctorId = 1,
                Date = from,
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(9, 0, 0),
            };
            mockDataSource
                .Setup(appointmentRepository => appointmentRepository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment> { appointment });

            var appointments = await service.GetAppointmentsInRangeAsync(1, from, to);

            Assert.Empty(appointments);
        }

        [Fact]
        public async Task GetAppointmentsInRangeAsync_IncludesAppointment_WhenItFallsWithinRange()
        {
            var from = new DateTime(2025, 6, 11);
            var to = from.AddDays(1);
            var appointment = new Appointment
            {
                DoctorId = 1,
                Date = from,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 0, 0),
            };
            mockDataSource
                .Setup(appointmentRepository => appointmentRepository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment> { appointment });

            var appointments = await service.GetAppointmentsInRangeAsync(1, from, to);

            Assert.Single(appointments);
        }

        [Fact]
        public async Task GetAppointmentsInRangeAsync_ExcludesAppointmentFromDifferentDoctor()
        {
            var from = new DateTime(2025, 6, 11);
            var to = from.AddDays(1);
            var appointment = new Appointment
            {
                DoctorId = 99,
                Date = from,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 0, 0),
            };
            mockDataSource
                .Setup(appointmentRepository => appointmentRepository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment> { appointment });

            var appointments = await service.GetAppointmentsInRangeAsync(1, from, to);

            Assert.Empty(appointments);
        }


        [Fact]
        public async Task GetShiftsForStaffInRangeAsync_ReturnsShifts_ExcludingCancelled()
        {
            var from = new DateTime(2025, 6, 11);
            var to = from.AddDays(1);
            var staff = new Pharmacyst(1, "Test", "Staff", string.Empty, true, "General", 1);
            var scheduled = new Shift(1, staff, "Ward A", from.AddHours(8), from.AddHours(16), ShiftStatus.SCHEDULED);
            var cancelled = new Shift(2, staff, "Ward B", from.AddHours(9), from.AddHours(17), ShiftStatus.CANCELLED);

            mockShiftRepository
                .Setup(shiftRepository => shiftRepository.GetAllShifts())
                .Returns(new List<Shift> { scheduled, cancelled });

            var shiftsForStaff = await service.GetShiftsForStaffInRangeAsync(1, from, to);

            Assert.Single(shiftsForStaff);
            Assert.Equal(1, shiftsForStaff[0].Id);
        }

        [Fact]
        public async Task GetShiftsForStaffInRangeAsync_ReturnsShiftsOrderedByStartTime()
        {
            var from = new DateTime(2025, 6, 11);
            var to = from.AddDays(1);
            var staff = new Pharmacyst(1, "Test", "Staff", string.Empty, true, "General", 1);
            var later = new Shift(1, staff, "Ward A", from.AddHours(14), from.AddHours(22), ShiftStatus.SCHEDULED);
            var earlier = new Shift(2, staff, "Ward B", from.AddHours(6), from.AddHours(14), ShiftStatus.SCHEDULED);

            mockShiftRepository
                .Setup(shiftRepository => shiftRepository.GetAllShifts())
                .Returns(new List<Shift> { later, earlier });

            var shiftsForStaff = await service.GetShiftsForStaffInRangeAsync(1, from, to);

            Assert.Equal(2, shiftsForStaff[0].Id);
            Assert.Equal(1, shiftsForStaff[1].Id);
        }

        [Fact]
        public async Task GetShiftsForStaffInRangeAsync_ReturnsEmpty_WhenRepositoryReturnsNoShifts()
        {
            var from = new DateTime(2025, 6, 11);
            var to = from.AddDays(1);

            mockShiftRepository
                .Setup(shiftRepository => shiftRepository.GetAllShifts())
                .Returns(new List<Shift>());

            var shiftsForStaff = await service.GetShiftsForStaffInRangeAsync(1, from, to);

            Assert.Empty(shiftsForStaff);
        }

        [Fact]
        public async Task GetShiftsForStaffInRangeAsync_FiltersByDoctor()
        {
            var from = new DateTime(2025, 6, 11);
            var to = from.AddDays(1);
            var ownStaff = new Pharmacyst(7, "Own", "Staff", string.Empty, true, "General", 1);
            var otherStaff = new Pharmacyst(99, "Other", "Staff", string.Empty, true, "General", 1);
            var ownShift = new Shift(1, ownStaff, "Ward A", from.AddHours(8), from.AddHours(16), ShiftStatus.SCHEDULED);
            var otherShift = new Shift(2, otherStaff, "Ward B", from.AddHours(8), from.AddHours(16), ShiftStatus.SCHEDULED);

            mockShiftRepository
                .Setup(shiftRepository => shiftRepository.GetAllShifts())
                .Returns(new List<Shift> { ownShift, otherShift });

            var shiftsForStaff = await service.GetShiftsForStaffInRangeAsync(7, from, to);

            Assert.Single(shiftsForStaff);
            Assert.Equal(1, shiftsForStaff[0].Id);
        }
    }
}
