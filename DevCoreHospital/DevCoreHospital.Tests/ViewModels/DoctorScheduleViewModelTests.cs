using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using DevCoreHospital.Views.Shell;
using Moq;

namespace DevCoreHospital.Tests.ViewModels
{
    public class DoctorScheduleViewModelTests
    {
        private readonly Mock<ICurrentUserService> mockCurrentUser;
        private readonly Mock<IDoctorAppointmentService> mockAppointmentService;
        private readonly DialogPresenter dialogPresenter = new DialogPresenter();
        private readonly DoctorScheduleViewModel viewModel;

        private static readonly DoctorScheduleViewModel.DoctorOption TestDoctor =
            new() { DoctorId = 1, DoctorName = "Ana Pop" };

        private static readonly Pharmacyst DummyStaff =
            new Pharmacyst(1, "Test", "Staff", string.Empty, true, "General", 1);

        public DoctorScheduleViewModelTests()
        {
            mockCurrentUser = new Mock<ICurrentUserService>();
            mockCurrentUser.Setup(currentUser => currentUser.Role).Returns("Doctor");
            mockCurrentUser.Setup(currentUser => currentUser.UserId).Returns(1);

            mockAppointmentService = new Mock<IDoctorAppointmentService>();
            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetAppointmentsInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Appointment>());

            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetShiftsForStaffInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift>());

            viewModel = new DoctorScheduleViewModel(
                mockCurrentUser.Object,
                mockAppointmentService.Object,
                dialogPresenter);
        }


        [Fact]
        public void IsDoctor_IsTrue_WhenRoleIsDoctor()
        {
            mockCurrentUser.Setup(currentUser => currentUser.Role).Returns("Doctor");

            Assert.True(viewModel.IsDoctor);
        }

        [Fact]
        public void IsDoctor_IsTrue_WhenRoleIsAdmin()
        {
            mockCurrentUser.Setup(currentUser => currentUser.Role).Returns("Admin");

            Assert.True(viewModel.IsDoctor);
        }

        [Fact]
        public void IsDoctor_IsFalse_WhenRoleIsNeitherDoctorNorAdmin()
        {
            mockCurrentUser.Setup(currentUser => currentUser.Role).Returns("Nurse");

            Assert.False(viewModel.IsDoctor);
        }


        [Fact]
        public async Task LoadAsync_SetsAccessDeniedErrorMessage_WhenRoleIsNotDoctorOrAdmin()
        {
            mockCurrentUser.Setup(currentUser => currentUser.Role).Returns("Nurse");

            await viewModel.LoadAsync();

            Assert.Equal("Access denied. Only doctors can view schedule.", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task LoadAsync_ClearsAppointments_WhenAccessIsDenied()
        {
            mockCurrentUser.Setup(currentUser => currentUser.Role).Returns("Nurse");

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Appointments);
        }

        [Fact]
        public async Task LoadAsync_ClearsShifts_WhenAccessIsDenied()
        {
            mockCurrentUser.Setup(currentUser => currentUser.Role).Returns("Nurse");

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
                .Setup(appointmentService => appointmentService.GetAppointmentsInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Appointment> { appointment });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Single(viewModel.Appointments);
        }

        [Fact]
        public async Task LoadAsync_CallsServiceWithDailyRange_WhenInDailyMode()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            DateTime capturedFrom = default;
            DateTime capturedTo = default;

            void CaptureDateRange(int _, DateTime from, DateTime to)
            {
                capturedFrom = from;
                capturedTo = to;
            }
            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetAppointmentsInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Callback<int, DateTime, DateTime>(CaptureDateRange)
                .ReturnsAsync(new List<Appointment>());

            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;
            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Equal(selectedDate.Date, capturedFrom);
            Assert.Equal(selectedDate.Date.AddDays(1), capturedTo);
        }


        [Fact]
        public async Task LoadAsync_IncludesShift_WhenServiceReturnsShiftInWeeklyMode()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var shiftOnFriday = new Shift(
                1, DummyStaff, "Ward A",
                new DateTime(2025, 6, 13, 8, 0, 0),
                new DateTime(2025, 6, 13, 16, 0, 0),
                ShiftStatus.SCHEDULED);

            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetShiftsForStaffInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift> { shiftOnFriday });

            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;
            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Single(viewModel.Shifts);
        }


        [Fact]
        public async Task LoadAsync_DisplaysAllAppointmentsReturnedByService()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var appointments = new List<Appointment>
            {
                new Appointment { DoctorId = 1, Date = selectedDate, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0) },
                new Appointment { DoctorId = 1, Date = selectedDate, StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 0, 0) },
            };

            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetAppointmentsInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(appointments);

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Equal(2, viewModel.Appointments.Count);
        }


        [Fact]
        public async Task LoadAsync_ExcludesShift_WhenServiceDoesNotReturnIt()
        {
            var selectedDate = new DateTime(2025, 6, 11);

            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetShiftsForStaffInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift>());

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Shifts);
        }

        [Fact]
        public async Task LoadAsync_IncludesShift_WhenServiceReturnsIt()
        {
            var selectedDate = new DateTime(2025, 6, 11);
            var scheduledShift = new Shift(
                1, DummyStaff, "Ward A",
                new DateTime(2025, 6, 11, 8, 0, 0),
                new DateTime(2025, 6, 11, 16, 0, 0),
                ShiftStatus.SCHEDULED);

            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetShiftsForStaffInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift> { scheduledShift });

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
                .Setup(appointmentService => appointmentService.GetAppointmentsInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
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

            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetShiftsForStaffInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift> { shift });

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.False(viewModel.IsEmpty);
        }

        [Fact]
        public async Task IsEmpty_IsFalse_WhenErrorMessageIsSet()
        {
            mockCurrentUser.Setup(currentUser => currentUser.Role).Returns("Nurse");

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
            var date = new DateTime(2025, 6, 11);
            var expected = date.ToString("dddd, dd MMM yyyy", CultureInfo.GetCultureInfo("en-US"));

            viewModel.SelectedDate = date;
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;

            Assert.Equal(expected, viewModel.SelectedDateText);
        }

        [Fact]
        public void SelectedDateText_InWeeklyMode_FormatsAsWeekOf()
        {
            var wednesday = new DateTime(2025, 6, 11);
            var monday = new DateTime(2025, 6, 9);
            var expected = $"Week of {monday.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-US"))}";

            viewModel.SelectedDate = wednesday;
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;

            Assert.Equal(expected, viewModel.SelectedDateText);
        }

        [Fact]
        public void TodayCommand_SetsSelectedDateToToday()
        {
            viewModel.SelectedDate = new DateTime(2020, 1, 1);

            viewModel.TodayCommand.Execute(null);

            Assert.Equal(DateTime.Today, viewModel.SelectedDate);
        }

        [Fact]
        public void NextDayCommand_AdvancesOneDayInDailyMode()
        {
            var date = new DateTime(2025, 6, 11);
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;
            viewModel.SelectedDate = date;

            viewModel.NextDayCommand.Execute(null);

            Assert.Equal(date.AddDays(1), viewModel.SelectedDate);
        }

        [Fact]
        public void NextDayCommand_AdvancesSevenDaysInWeeklyMode()
        {
            var date = new DateTime(2025, 6, 11);
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;
            viewModel.SelectedDate = date;

            viewModel.NextDayCommand.Execute(null);

            Assert.Equal(date.AddDays(7), viewModel.SelectedDate);
        }

        [Fact]
        public void PreviousDayCommand_GoesBackOneDayInDailyMode()
        {
            var date = new DateTime(2025, 6, 11);
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;
            viewModel.SelectedDate = date;

            viewModel.PreviousDayCommand.Execute(null);

            Assert.Equal(date.AddDays(-1), viewModel.SelectedDate);
        }

        [Fact]
        public void PreviousDayCommand_GoesBackSevenDaysInWeeklyMode()
        {
            var date = new DateTime(2025, 6, 11);
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;
            viewModel.SelectedDate = date;

            viewModel.PreviousDayCommand.Execute(null);

            Assert.Equal(date.AddDays(-7), viewModel.SelectedDate);
        }

        [Fact]
        public void DailyModeCommand_SetsViewModeToDaily()
        {
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Weekly;

            viewModel.DailyModeCommand.Execute(null);

            Assert.Equal(DoctorScheduleViewModel.ScheduleViewMode.Daily, viewModel.ViewMode);
        }

        [Fact]
        public void WeeklyModeCommand_SetsViewModeToWeekly()
        {
            viewModel.ViewMode = DoctorScheduleViewModel.ScheduleViewMode.Daily;

            viewModel.WeeklyModeCommand.Execute(null);

            Assert.Equal(DoctorScheduleViewModel.ScheduleViewMode.Weekly, viewModel.ViewMode);
        }

        [Fact]
        public async Task InitializeAsync_SetsNoDoctorsErrorMessage_WhenServiceReturnsEmptyList()
        {
            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetAllDoctorsAsync())
                .ReturnsAsync(new List<(int DoctorId, string DoctorName)>());

            await viewModel.InitializeAsync();

            Assert.Equal("No doctors available.", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task LoadAsync_SetsErrorMessage_WhenAppointmentServiceThrows()
        {
            mockAppointmentService
                .Setup(appointmentService => appointmentService.GetAppointmentsInRangeAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("DB error"));

            viewModel.SelectedDoctor = TestDoctor;

            await viewModel.LoadAsync();

            Assert.Contains("DB error", viewModel.ErrorMessage);
        }
    }
}
