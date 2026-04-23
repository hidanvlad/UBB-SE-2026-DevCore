using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Moq;

namespace DevCoreHospital.Tests.ViewModels
{
    public class DoctorScheduleViewModelTests
    {
        private readonly Mock<ICurrentUserService> mockCurrentUser;
        private readonly Mock<IDoctorAppointmentService> mockAppointmentService;
        private readonly Mock<IShiftRepository> mockShiftRepository;
        private readonly Mock<IDialogService> mockDialogService = new Mock<IDialogService>();
        private readonly DoctorScheduleViewModel viewModel;

        private static readonly DoctorScheduleViewModel.DoctorOption TestDoctor =
            new() { DoctorId = 1, DoctorName = "Ana Pop" };

        private static readonly Pharmacyst DummyStaff =
            new Pharmacyst(1, "Test", "Staff", string.Empty, true, "General", 1);

        public DoctorScheduleViewModelTests()
        {
            mockCurrentUser = new Mock<ICurrentUserService>();
            mockCurrentUser.Setup(u => u.Role).Returns("Doctor");
            mockCurrentUser.Setup(u => u.UserId).Returns(1);

            mockAppointmentService = new Mock<IDoctorAppointmentService>();
            mockAppointmentService
                .Setup(s => s.GetUpcomingAppointmentsAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment>());

            mockShiftRepository = new Mock<IShiftRepository>();
            mockShiftRepository
                .Setup(r => r.GetShiftsForStaffInRange(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            viewModel = new DoctorScheduleViewModel(
                mockCurrentUser.Object,
                mockAppointmentService.Object,
                mockShiftRepository.Object,
                mockDialogService.Object);
        }


        [Fact]
        public void IsDoctor_IsTrue_WhenRoleIsDoctor()
        {
            mockCurrentUser.Setup(u => u.Role).Returns("Doctor");

            Assert.True(viewModel.IsDoctor);
        }

        [Fact]
        public void IsDoctor_IsTrue_WhenRoleIsAdmin()
        {
            mockCurrentUser.Setup(u => u.Role).Returns("Admin");

            Assert.True(viewModel.IsDoctor);
        }

        [Fact]
        public void IsDoctor_IsFalse_WhenRoleIsNeitherDoctorNorAdmin()
        {
            mockCurrentUser.Setup(u => u.Role).Returns("Nurse");

            Assert.False(viewModel.IsDoctor);
        }


        [Fact]
        public async Task LoadAsync_SetsAccessDeniedErrorMessage_WhenRoleIsNotDoctorOrAdmin()
        {
            mockCurrentUser.Setup(u => u.Role).Returns("Nurse");

            await viewModel.LoadAsync();

            Assert.Equal("Access denied. Only doctors can view schedule.", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task LoadAsync_ClearsAppointments_WhenAccessIsDenied()
        {
            mockCurrentUser.Setup(u => u.Role).Returns("Nurse");

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Appointments);
        }

        [Fact]
        public async Task LoadAsync_ClearsShifts_WhenAccessIsDenied()
        {
            mockCurrentUser.Setup(u => u.Role).Returns("Nurse");

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Shifts);
        }


        [Fact]
        public async Task LoadAsync_ResultsInEmptyAppointments_WhenNoDoctorIsSelected()
        {
            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Appointments);
        }

        [Fact]
        public async Task LoadAsync_ResultsInEmptyShifts_WhenNoDoctorIsSelected()
        {
            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Shifts);
        }


        [Fact]
        public async Task LoadAsync_IncludesAppointment_WhenItFallsOnSelectedDateInDailyMode()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var appointment = new Appointment
            {
                DoctorId = 1,
                Date = selectedDate,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 0, 0),
            };

            mockAppointmentService
                .Setup(s => s.GetUpcomingAppointmentsAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Single(viewModel.Appointments);
        }

        [Fact]
        public async Task LoadAsync_ExcludesAppointment_WhenItFallsTwoDaysAfterSelectedDateInDailyMode()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var appointment = new Appointment
            {
                DoctorId = 1,
                Date = selectedDate.AddDays(2),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 0, 0),
            };

            mockAppointmentService
                .Setup(s => s.GetUpcomingAppointmentsAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Appointments);
        }


        [Fact]
        public async Task LoadAsync_IncludesShift_WhenItFallsWithinCurrentWeekInWeeklyMode()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var shiftOnFriday = new Shift(
                1, DummyStaff, "Ward A",
                new DateTime(2025, 6, 13, 8, 0, 0),
                new DateTime(2025, 6, 13, 16, 0, 0),
                ShiftStatus.SCHEDULED);

            mockShiftRepository
                .Setup(r => r.GetShiftsForStaffInRange(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift> { shiftOnFriday });

            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;
            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Single(viewModel.Shifts);
        }


        [Fact]
        public async Task LoadAsync_ExcludesAppointment_WhenEndTimeEqualsStartTime()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var appointment = new Appointment
            {
                DoctorId = 1,
                Date = selectedDate,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(10, 0, 0), 
            };

            mockAppointmentService
                .Setup(s => s.GetUpcomingAppointmentsAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Appointments);
        }

        [Fact]
        public async Task LoadAsync_ExcludesAppointment_WhenEndTimeIsBeforeStartTime()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var appointment = new Appointment
            {
                DoctorId = 1,
                Date = selectedDate,
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(9, 0, 0),
            };

            mockAppointmentService
                .Setup(s => s.GetUpcomingAppointmentsAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Appointments);
        }


        [Fact]
        public async Task LoadAsync_ExcludesShift_WhenStatusIsCancelled()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var cancelledShift = new Shift(
                1, DummyStaff, "Ward A",
                new DateTime(2025, 6, 11, 8, 0, 0),
                new DateTime(2025, 6, 11, 16, 0, 0),
                ShiftStatus.CANCELLED);

            mockShiftRepository
                .Setup(r => r.GetShiftsForStaffInRange(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift> { cancelledShift });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Shifts);
        }

        [Fact]
        public async Task LoadAsync_IncludesShift_WhenStatusIsScheduled()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var scheduledShift = new Shift(
                1, DummyStaff, "Ward A",
                new DateTime(2025, 6, 11, 8, 0, 0),
                new DateTime(2025, 6, 11, 16, 0, 0),
                ShiftStatus.SCHEDULED);

            mockShiftRepository
                .Setup(r => r.GetShiftsForStaffInRange(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift> { scheduledShift });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Single(viewModel.Shifts);
        }


        [Fact]
        public async Task IsEmpty_IsTrue_WhenNoDoctorSelectedAndNoErrorAndNotLoading()
        {
            await viewModel.LoadAsync();

            Assert.True(viewModel.IsEmpty);
        }

        [Fact]
        public async Task IsEmpty_IsFalse_WhenAppointmentsAreLoaded()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var appointment = new Appointment
            {
                DoctorId = 1,
                Date = selectedDate,
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 0, 0),
            };

            mockAppointmentService
                .Setup(s => s.GetUpcomingAppointmentsAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.False(viewModel.IsEmpty);
        }

        [Fact]
        public async Task IsEmpty_IsFalse_WhenShiftsAreLoaded()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var shift = new Shift(
                1, DummyStaff, "Ward A",
                new DateTime(2025, 6, 11, 8, 0, 0),
                new DateTime(2025, 6, 11, 16, 0, 0),
                ShiftStatus.SCHEDULED);

            mockShiftRepository
                .Setup(r => r.GetShiftsForStaffInRange(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift> { shift });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.False(viewModel.IsEmpty);
        }

        [Fact]
        public async Task IsEmpty_IsFalse_WhenErrorMessageIsSet()
        {
            mockCurrentUser.Setup(u => u.Role).Returns("Nurse");

            await viewModel.LoadAsync();

            Assert.False(viewModel.IsEmpty);
        }

        [Theory]
        [InlineData("Ana Pop", "Ana", "Pop")]
        [InlineData("John", "John", "")]
        [InlineData("Mary Jane Watson", "Mary", "Watson")]
        public void DoctorOption_SplitFirstLast_ExtractsCorrectParts(string fullName, string expectedFirst, string expectedLast)
        {
            var (first, last) = DoctorScheduleViewModel.DoctorOption.SplitFirstLast(fullName);

            Assert.Equal(expectedFirst, first);
            Assert.Equal(expectedLast, last);
        }

        [Fact]
        public void DoctorOption_SplitFirstLast_ReturnsEmpty_WhenInputIsNullOrWhitespace()
        {
            var (first1, last1) = DoctorScheduleViewModel.DoctorOption.SplitFirstLast(null);
            var (first2, last2) = DoctorScheduleViewModel.DoctorOption.SplitFirstLast("   ");

            Assert.Equal(string.Empty, first1);
            Assert.Equal(string.Empty, last1);
            Assert.Equal(string.Empty, first2);
            Assert.Equal(string.Empty, last2);
        }

        [Fact]
        public void DoctorOption_DisplayName_CombinesFirstAndLastName()
        {
            var option = new DoctorScheduleViewModel.DoctorOption
            {
                FirstName = "Ana",
                LastName = "Pop"
            };

            Assert.Equal("Ana Pop", option.DisplayName);
        }

        [Fact]
        public void DoctorOption_DisplayName_SkipsEmptyParts()
        {
            var option = new DoctorScheduleViewModel.DoctorOption
            {
                FirstName = "John",
                LastName = string.Empty
            };

            Assert.Equal("John", option.DisplayName);
        }


        [Fact]
        public void IsDaily_IsTrue_WhenViewModeIsDaily()
        {
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;

            Assert.True(viewModel.IsDaily);
        }

        [Fact]
        public void IsWeekly_IsTrue_WhenViewModeIsWeekly()
        {
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;

            Assert.True(viewModel.IsWeekly);
        }

        [Fact]
        public void PreviousButtonText_IsPrevious_InDailyMode()
        {
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;

            Assert.Equal("Previous", viewModel.PreviousButtonText);
        }

        [Fact]
        public void NextButtonText_IsNext_InDailyMode()
        {
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;

            Assert.Equal("Next", viewModel.NextButtonText);
        }

        [Fact]
        public void PreviousButtonText_IsPreviousWeek_InWeeklyMode()
        {
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;

            Assert.Equal("Previous Week", viewModel.PreviousButtonText);
        }

        [Fact]
        public void NextButtonText_IsNextWeek_InWeeklyMode()
        {
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;

            Assert.Equal("Next Week", viewModel.NextButtonText);
        }

        [Fact]
        public void SelectedDateText_InDailyMode_FormatsAsFullDate()
        {
            // Arrange
            var date = new DateTime(2025, 6, 11);
            var expected = date.ToString("dddd, dd MMM yyyy", CultureInfo.GetCultureInfo("en-US"));

            // Act
            viewModel.SelectedDate = date;
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;

            // Assert
            Assert.Equal(expected, viewModel.SelectedDateText);
        }

        [Fact]
        public void SelectedDateText_InWeeklyMode_FormatsAsWeekOf()
        {
            // Arrange
            var wednesday = new DateTime(2025, 6, 11);
            var monday = new DateTime(2025, 6, 9);
            var expected = $"Week of {monday.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-US"))}";

            // Act
            viewModel.SelectedDate = wednesday;
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;

            // Assert
            Assert.Equal(expected, viewModel.SelectedDateText);
        }

        [Fact]
        public void TodayCommand_SetsSelectedDateToToday()
        {
            // Arrange
            viewModel.SelectedDate = new DateTime(2020, 1, 1);

            // Act
            viewModel.TodayCommand.Execute(null);

            // Assert
            Assert.Equal(DateTime.Today, viewModel.SelectedDate);
        }

        [Fact]
        public void NextDayCommand_AdvancesOneDayInDailyMode()
        {
            // Arrange
            var date = new DateTime(2025, 6, 11);
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;
            viewModel.SelectedDate = date;

            // Act
            viewModel.NextDayCommand.Execute(null);

            // Assert
            Assert.Equal(date.AddDays(1), viewModel.SelectedDate);
        }

        [Fact]
        public void NextDayCommand_AdvancesSevenDaysInWeeklyMode()
        {
            // Arrange
            var date = new DateTime(2025, 6, 11);
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;
            viewModel.SelectedDate = date;

            // Act
            viewModel.NextDayCommand.Execute(null);

            // Assert
            Assert.Equal(date.AddDays(7), viewModel.SelectedDate);
        }

        [Fact]
        public void PreviousDayCommand_GoesBackOneDayInDailyMode()
        {
            // Arrange
            var date = new DateTime(2025, 6, 11);
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;
            viewModel.SelectedDate = date;

            // Act
            viewModel.PreviousDayCommand.Execute(null);

            // Assert
            Assert.Equal(date.AddDays(-1), viewModel.SelectedDate);
        }

        [Fact]
        public void PreviousDayCommand_GoesBackSevenDaysInWeeklyMode()
        {
            // Arrange
            var date = new DateTime(2025, 6, 11);
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;
            viewModel.SelectedDate = date;

            // Act
            viewModel.PreviousDayCommand.Execute(null);

            // Assert
            Assert.Equal(date.AddDays(-7), viewModel.SelectedDate);
        }

        [Fact]
        public void DailyModeCommand_SetsViewModeToDaily()
        {
            // Arrange
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;

            // Act
            viewModel.DailyModeCommand.Execute(null);

            // Assert
            Assert.Equal(DoctorScheduleViewModel.ScheduleViewMode.Daily, viewModel.ViewMode);
        }

        [Fact]
        public void WeeklyModeCommand_SetsViewModeToWeekly()
        {
            // Arrange
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;

            // Act
            viewModel.WeeklyModeCommand.Execute(null);

            // Assert
            Assert.Equal(DoctorScheduleViewModel.ScheduleViewMode.Weekly, viewModel.ViewMode);
        }

        [Fact]
        public async Task InitializeAsync_SetsNoDoctorsErrorMessage_WhenServiceReturnsEmptyList()
        {
            // Arrange
            mockAppointmentService
                .Setup(s => s.GetAllDoctorsAsync())
                .ReturnsAsync(new List<(int DoctorId, string DoctorName)>());

            // Act
            await viewModel.InitializeAsync();

            // Assert
            Assert.Equal("No doctors available.", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task LoadAsync_SetsErrorMessage_WhenAppointmentServiceThrows()
        {
            // Arrange
            mockAppointmentService
                .Setup(s => s.GetUpcomingAppointmentsAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("DB error"));

            viewModel.SelectedDoctor = TestDoctor;

            // Act
            await viewModel.LoadAsync();

            // Assert
            Assert.Contains("DB error", viewModel.ErrorMessage);
        }
    }
}
