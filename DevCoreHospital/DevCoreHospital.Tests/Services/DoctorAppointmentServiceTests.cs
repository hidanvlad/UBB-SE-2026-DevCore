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
        private readonly Mock<IShiftRepository> mockShiftRepository;
        private readonly DoctorAppointmentService service;

        public DoctorAppointmentServiceTests()
        {
            mockDataSource = new Mock<IAppointmentRepository>();
            mockShiftRepository = new Mock<IShiftRepository>();
            service = new DoctorAppointmentService(mockDataSource.Object, mockShiftRepository.Object);
        }


        [Fact]
        public async Task BookAppointmentAsync_AddsAppointment_WithGivenAppointmentObject()
        {
            var appointment = new Appointment { Id = 1, DoctorId = 10 };

            await service.BookAppointmentAsync(appointment);

            mockDataSource.Verify(x => x.AddAppointmentAsync(appointment), Times.Once);
        }

        [Fact]
        public async Task BookAppointmentAsync_SetsDoctorStatus_ToInExamination()
        {
            var appointment = new Appointment { Id = 1, DoctorId = 10 };

            await service.BookAppointmentAsync(appointment);

            mockDataSource.Verify(x => x.UpdateDoctorStatusAsync(10, "IN_EXAMINATION"), Times.Once);
        }


        [Fact]
        public async Task FinishAppointmentAsync_SetsAppointmentStatus_ToFinished()
        {
            var appointment = new Appointment { Id = 5, DoctorId = 10 };
            mockDataSource.Setup(x => x.GetActiveAppointmentsCountForDoctorAsync(10)).ReturnsAsync(1);

            await service.FinishAppointmentAsync(appointment);

            mockDataSource.Verify(x => x.UpdateAppointmentStatusAsync(5, "Finished"), Times.Once);
        }

        [Fact]
        public async Task FinishAppointmentAsync_SetsDoctorStatus_ToAvailable_WhenNoActiveAppointmentsRemain()
        {
            var appointment = new Appointment { Id = 5, DoctorId = 10 };
            mockDataSource.Setup(x => x.GetActiveAppointmentsCountForDoctorAsync(10)).ReturnsAsync(0);

            await service.FinishAppointmentAsync(appointment);

            mockDataSource.Verify(x => x.UpdateDoctorStatusAsync(10, "AVAILABLE"), Times.Once);
        }

        [Fact]
        public async Task FinishAppointmentAsync_DoesNotUpdateDoctorStatus_WhenActiveAppointmentsRemain()
        {
            var appointment = new Appointment { Id = 5, DoctorId = 10 };
            mockDataSource.Setup(x => x.GetActiveAppointmentsCountForDoctorAsync(10)).ReturnsAsync(2);

            await service.FinishAppointmentAsync(appointment);

            mockDataSource.Verify(x => x.UpdateDoctorStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
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
        public async Task CancelAppointmentAsync_ThrowsInvalidOperationException_WhenFinishedStatus_IsCaseDifferent()
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

            mockDataSource.Verify(x => x.UpdateAppointmentStatusAsync(3, "Canceled"), Times.Once);
        }

        [Fact]
        public async Task CancelAppointmentAsync_DoesNotUpdateStatus_WhenAppointmentIsAlreadyFinished()
        {
            var finishedAppointment = new Appointment { Id = 3, DoctorId = 10, Status = "Finished" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CancelAppointmentAsync(finishedAppointment));

            mockDataSource.Verify(x => x.UpdateAppointmentStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetAllDoctorsAsync_ReturnsResultFromDataSource()
        {
            IReadOnlyList<(int DoctorId, string DoctorName)> expected = new List<(int, string)>
            {
                (1, "Dr. Smith"),
                (2, "Dr. Jones")
            };
            mockDataSource.Setup(x => x.GetAllDoctorsAsync()).ReturnsAsync(expected);

            var result = await service.GetAllDoctorsAsync();

            Assert.Same(expected, result);
            mockDataSource.Verify(x => x.GetAllDoctorsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAppointmentDetailsAsync_CallsDataSourceWithCorrectId()
        {
            var expected = new Appointment { Id = 42, DoctorId = 5, PatientName = "Jane Doe" };
            mockDataSource.Setup(x => x.GetAppointmentDetailsAsync(42)).ReturnsAsync(expected);

            var result = await service.GetAppointmentDetailsAsync(42);

            Assert.Same(expected, result);
            mockDataSource.Verify(x => x.GetAppointmentDetailsAsync(42), Times.Once);
        }

        [Fact]
        public async Task GetAppointmentDetailsAsync_ReturnsNull_WhenNotFound()
        {
            mockDataSource.Setup(x => x.GetAppointmentDetailsAsync(99)).ReturnsAsync((Appointment?)null);

            var result = await service.GetAppointmentDetailsAsync(99);

            Assert.Null(result);
        }


        [Fact]
        public async Task CreateAppointmentAsync_AddsAppointment_WithScheduledStatusAndThirtyMinuteDuration()
        {
            var date = new DateTime(2025, 8, 1);
            var startTime = new TimeSpan(10, 0, 0);
            Appointment? captured = null;

            mockDataSource
                .Setup(x => x.AddAppointmentAsync(It.IsAny<Appointment>()))
                .Callback<Appointment>(a => captured = a);
            mockDataSource
                .Setup(x => x.GetActiveAppointmentsCountForDoctorAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            await service.CreateAppointmentAsync("PAT-1", 5, date, startTime);

            Assert.NotNull(captured);
            Assert.Equal("PAT-1", captured!.PatientName);
            Assert.Equal(5, captured.DoctorId);
            Assert.Equal(date.Date, captured.Date);
            Assert.Equal(startTime, captured.StartTime);
            Assert.Equal(startTime.Add(TimeSpan.FromMinutes(30)), captured.EndTime);
            Assert.Equal("Scheduled", captured.Status);
        }

        [Fact]
        public async Task CreateAppointmentAsync_SetsDoctorStatus_ToInExamination()
        {
            mockDataSource
                .Setup(x => x.GetActiveAppointmentsCountForDoctorAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            await service.CreateAppointmentAsync("PAT-1", 7, DateTime.Today, TimeSpan.Zero);

            mockDataSource.Verify(x => x.UpdateDoctorStatusAsync(7, "IN_EXAMINATION"), Times.Once);
        }


        [Fact]
        public async Task FinishAppointmentAsync_ThrowsInvalidOperationException_WhenAppointmentIsAlreadyFinished()
        {
            var finishedAppointment = new Appointment { Id = 5, DoctorId = 10, Status = "Finished" };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.FinishAppointmentAsync(finishedAppointment));

            Assert.Equal("This appointment is already finished.", ex.Message);
        }

        [Fact]
        public async Task FinishAppointmentAsync_ThrowsInvalidOperationException_WhenFinishedStatus_IsCaseDifferent()
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

            mockDataSource.Verify(x => x.UpdateAppointmentStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
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
                .Setup(x => x.GetUpcomingAppointmentsAsync(1, from, 0, It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            var result = await service.GetAppointmentsInRangeAsync(1, from, to);

            Assert.Empty(result);
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
                .Setup(x => x.GetUpcomingAppointmentsAsync(1, from, 0, It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            var result = await service.GetAppointmentsInRangeAsync(1, from, to);

            Assert.Empty(result);
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
                .Setup(x => x.GetUpcomingAppointmentsAsync(1, from, 0, It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            var result = await service.GetAppointmentsInRangeAsync(1, from, to);

            Assert.Single(result);
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
                .Setup(x => x.GetUpcomingAppointmentsAsync(1, from, 0, It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            var result = await service.GetAppointmentsInRangeAsync(1, from, to);

            Assert.Empty(result);
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
                .Setup(r => r.GetShiftsForStaffInRange(1, from, to))
                .Returns(new List<Shift> { scheduled, cancelled });

            var result = await service.GetShiftsForStaffInRangeAsync(1, from, to);

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
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
                .Setup(r => r.GetShiftsForStaffInRange(1, from, to))
                .Returns(new List<Shift> { later, earlier });

            var result = await service.GetShiftsForStaffInRangeAsync(1, from, to);

            Assert.Equal(2, result[0].Id);
            Assert.Equal(1, result[1].Id);
        }

        [Fact]
        public async Task GetShiftsForStaffInRangeAsync_ReturnsEmpty_WhenRepositoryReturnsNoShifts()
        {
            var from = new DateTime(2025, 6, 11);
            var to = from.AddDays(1);

            mockShiftRepository
                .Setup(r => r.GetShiftsForStaffInRange(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            var result = await service.GetShiftsForStaffInRangeAsync(1, from, to);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetShiftsForStaffInRangeAsync_PassesCorrectRangeToRepository()
        {
            var from = new DateTime(2025, 6, 9);
            var to = new DateTime(2025, 6, 16);

            mockShiftRepository
                .Setup(r => r.GetShiftsForStaffInRange(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            await service.GetShiftsForStaffInRangeAsync(7, from, to);

            mockShiftRepository.Verify(r => r.GetShiftsForStaffInRange(7, from, to), Times.Once);
        }
    }
}
