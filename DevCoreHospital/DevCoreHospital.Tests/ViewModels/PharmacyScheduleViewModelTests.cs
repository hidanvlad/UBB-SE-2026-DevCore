using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Pharmacy;
using Moq;

namespace DevCoreHospital.Tests.ViewModels
{
    public class PharmacyScheduleViewModelTests
    {
        private readonly Mock<ICurrentUserService> userMock;
        private readonly Mock<IPharmacyScheduleService> serviceMock;

        public PharmacyScheduleViewModelTests()
        {
            userMock = new Mock<ICurrentUserService>();
            serviceMock = new Mock<IPharmacyScheduleService>();

            userMock.Setup(currentUser => currentUser.Role).Returns("Pharmacist");
            userMock.Setup(currentUser => currentUser.UserId).Returns(1);

            serviceMock
                .Setup(scheduleService => scheduleService.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift>());
        }

        private PharmacyScheduleViewModel CreateViewModel()
            => new PharmacyScheduleViewModel(userMock.Object, serviceMock.Object);

        private static void SetSelectedPharmacist(PharmacyScheduleViewModel viewModel, PharmacyScheduleViewModel.PharmacistOption? pharmacist)
        {
            var field = typeof(PharmacyScheduleViewModel)
                .GetField("selectedPharmacist", BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(viewModel, pharmacist);
        }

        private static void SetIsWeeklyView(PharmacyScheduleViewModel viewModel, bool value)
        {
            var field = typeof(PharmacyScheduleViewModel)
                .GetField("isWeeklyView", BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(viewModel, value);
        }

        private static void SetAnchorDate(PharmacyScheduleViewModel viewModel, DateTime date)
        {
            var field = typeof(PharmacyScheduleViewModel)
                .GetField("anchorDate", BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(viewModel, date);
        }

        private static PharmacyScheduleViewModel.PharmacistOption MakePharmacist(int id = 1, string name = "Alice Smith")
            => new PharmacyScheduleViewModel.PharmacistOption { StaffId = id, PharmacistName = name };

        [Fact]
        public void IsAccessDenied_IsTrue_WhenRoleIsNotPharmacistOrAdmin()
        {
            userMock.Setup(currentUser => currentUser.Role).Returns("Doctor");
            var viewModel = CreateViewModel();

            Assert.True(viewModel.IsAccessDenied);
            Assert.False(viewModel.IsPharmacist);
        }

        [Fact]
        public void IsPharmacist_IsTrue_WhenRoleIsPharmacist()
        {
            userMock.Setup(currentUser => currentUser.Role).Returns("Pharmacist");
            var viewModel = CreateViewModel();

            Assert.True(viewModel.IsPharmacist);
        }

        [Fact]
        public void IsPharmacist_IsTrue_WhenRoleIsAdmin()
        {
            userMock.Setup(currentUser => currentUser.Role).Returns("Admin");
            var viewModel = CreateViewModel();

            Assert.True(viewModel.IsPharmacist);
        }

        [Fact]
        public async Task LoadAsync_WhenAccessDenied_ClearsShiftsAndDoesNotCallService()
        {
            userMock.Setup(currentUser => currentUser.Role).Returns("Doctor");
            var viewModel = CreateViewModel();

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Shifts);
            serviceMock.Verify(
                scheduleService => scheduleService.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()),
                Times.Never);
        }

        [Fact]
        public async Task LoadAsync_ReturnsEarly_WhenNoPharmacistSelected()
        {
            var viewModel = CreateViewModel();
            SetSelectedPharmacist(viewModel, null);

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.Shifts);
            serviceMock.Verify(
                scheduleService => scheduleService.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()),
                Times.Never);
        }

        [Fact]
        public async Task LoadAsync_UsesCorrectDailyRange_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 16); // Wednesday
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, false);
            SetSelectedPharmacist(viewModel, MakePharmacist(id: 1));

            await viewModel.LoadAsync();

            var expectedStart = anchor.Date;
            var expectedEnd = anchor.Date.AddDays(1);
            serviceMock.Verify(
                scheduleService => scheduleService.GetShiftsAsync(1, expectedStart, expectedEnd),
                Times.Once);
        }

        [Fact]
        public async Task LoadAsync_UsesCorrectWeeklyRange_WhenIsWeeklyView()
        {
            var anchor = new DateTime(2025, 4, 16);
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, true);
            SetSelectedPharmacist(viewModel, MakePharmacist(id: 1));

            await viewModel.LoadAsync();

            var expectedStart = new DateTime(2025, 4, 14);
            var expectedEnd = new DateTime(2025, 4, 21);   // +7 days
            serviceMock.Verify(
                scheduleService => scheduleService.GetShiftsAsync(1, expectedStart, expectedEnd),
                Times.Once);
        }

        [Fact]
        public async Task LoadAsync_PopulatesShifts_WhenServiceReturnsData()
        {
            var start = new DateTime(2025, 4, 16, 8, 0, 0);
            var end = new DateTime(2025, 4, 16, 16, 0, 0);
            var staff = new Doctor { StaffID = 1, FirstName = "Test", LastName = "Doc" };
            var shift = new Shift(1, staff, "Pharmacy A", start, end, ShiftStatus.SCHEDULED);

            serviceMock
                .Setup(scheduleService => scheduleService.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift> { shift });

            var viewModel = CreateViewModel();
            SetSelectedPharmacist(viewModel, MakePharmacist(id: 1));

            await viewModel.LoadAsync();

            Assert.Single(viewModel.Shifts);
        }

        [Fact]
        public async Task LoadAsync_SetsErrorMessage_WhenServiceThrows()
        {
            serviceMock
                .Setup(scheduleService => scheduleService.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("DB error"));

            var viewModel = CreateViewModel();
            SetSelectedPharmacist(viewModel, MakePharmacist());

            await viewModel.LoadAsync();

            Assert.Contains("Failed to load pharmacy schedule", viewModel.ErrorMessage);
        }

        [Fact]
        public void HeaderSubtitle_ShowsWeekRange_WhenIsWeeklyView()
        {
            var anchor = new DateTime(2025, 4, 16);
            var startOfWeek = new DateTime(2025, 4, 14);
            var endOfWeek = new DateTime(2025, 4, 20);
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, true);

            var subtitle = viewModel.HeaderSubtitle;

            Assert.Contains(startOfWeek.ToString("dd MMM yyyy"), subtitle);
            Assert.Contains(endOfWeek.ToString("dd MMM yyyy"), subtitle);
        }

        [Fact]
        public void HeaderSubtitle_ShowsDayName_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 16);
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, false);

            Assert.Contains(anchor.ToString("dddd"), viewModel.HeaderSubtitle);
            Assert.Contains(anchor.ToString("dd MMM yyyy"), viewModel.HeaderSubtitle);
        }

        [Fact]
        public void SelectedDateText_ShowsWeekOf_WhenIsWeeklyView()
        {
            var anchor = new DateTime(2025, 4, 16);
            var startOfWeek = new DateTime(2025, 4, 14);
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, true);

            Assert.StartsWith("Week of ", viewModel.SelectedDateText);
            Assert.Contains(startOfWeek.ToString("dd MMM yyyy"), viewModel.SelectedDateText);
        }

        [Fact]
        public void SelectedDateText_ShowsDayDate_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 16);
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, false);

            Assert.Contains(anchor.ToString("dddd"), viewModel.SelectedDateText);
        }

        [Fact]
        public void IsDailyView_WhenWeeklyViewToggled_IsOppositeOfIsWeeklyView()
        {
            var viewModel = CreateViewModel();
            SetIsWeeklyView(viewModel, true);

            Assert.False(viewModel.IsDailyView);

            SetIsWeeklyView(viewModel, false);

            Assert.True(viewModel.IsDailyView);
        }

        [Fact]
        public void NextPeriodCommand_AdvancesAnchorByOneWeek_WhenIsWeeklyView()
        {
            var anchor = new DateTime(2025, 4, 14);
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, true);

            viewModel.NextPeriodCommand.Execute(null);

            Assert.Equal(anchor.AddDays(7), viewModel.AnchorDate);
        }

        [Fact]
        public void NextPeriodCommand_AdvancesAnchorByOneDay_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 14);
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, false);

            viewModel.NextPeriodCommand.Execute(null);

            Assert.Equal(anchor.AddDays(1), viewModel.AnchorDate);
        }

        [Fact]
        public void PreviousPeriodCommand_GoesBackOneWeek_WhenIsWeeklyView()
        {
            var anchor = new DateTime(2025, 4, 14);
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, true);

            viewModel.PreviousPeriodCommand.Execute(null);

            Assert.Equal(anchor.AddDays(-7), viewModel.AnchorDate);
        }

        [Fact]
        public void PreviousPeriodCommand_GoesBackOneDay_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 14);
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, anchor);
            SetIsWeeklyView(viewModel, false);

            viewModel.PreviousPeriodCommand.Execute(null);

            Assert.Equal(anchor.AddDays(-1), viewModel.AnchorDate);
        }

        [Fact]
        public void ShowDailyCommand_WhenExecuted_SetsIsDailyViewTrue()
        {
            var viewModel = CreateViewModel();
            SetIsWeeklyView(viewModel, true);

            viewModel.ShowDailyCommand.Execute(null);

            Assert.True(viewModel.IsDailyView);
            Assert.False(viewModel.IsWeeklyView);
        }

        [Fact]
        public void ShowWeeklyCommand_WhenExecuted_SetsIsWeeklyViewTrue()
        {
            var viewModel = CreateViewModel();
            SetIsWeeklyView(viewModel, false);

            viewModel.ShowWeeklyCommand.Execute(null);

            Assert.True(viewModel.IsWeeklyView);
            Assert.False(viewModel.IsDailyView);
        }

        [Fact]
        public void TodayCommand_WhenExecuted_SetsAnchorDateToToday()
        {
            var viewModel = CreateViewModel();
            SetAnchorDate(viewModel, new DateTime(2020, 1, 1));

            viewModel.TodayCommand.Execute(null);

            Assert.Equal(DateTime.Today, viewModel.AnchorDate);
        }

        [Fact]
        public async Task IsEmpty_IsTrue_WhenNoShiftsLoadedAndNoError()
        {
            serviceMock
                .Setup(scheduleService => scheduleService.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift>());

            var viewModel = CreateViewModel();
            SetSelectedPharmacist(viewModel, MakePharmacist());

            await viewModel.LoadAsync();

            Assert.True(viewModel.IsEmpty);
        }

        [Fact]
        public async Task IsEmpty_IsFalse_WhenShiftsAreLoaded()
        {
            var staff = new Doctor { StaffID = 1, FirstName = "T", LastName = "D" };
            var shift = new Shift(1, staff, "X", DateTime.Today, DateTime.Today.AddHours(8), ShiftStatus.SCHEDULED);
            serviceMock
                .Setup(scheduleService => scheduleService.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift> { shift });

            var viewModel = CreateViewModel();
            SetSelectedPharmacist(viewModel, MakePharmacist());

            await viewModel.LoadAsync();

            Assert.False(viewModel.IsEmpty);
        }

        [Fact]
        public async Task IsLoading_WhenLoadCompletes_IsFalse()
        {
            var viewModel = CreateViewModel();
            SetSelectedPharmacist(viewModel, MakePharmacist());

            await viewModel.LoadAsync();

            Assert.False(viewModel.IsLoading);
        }

        [Fact]
        public async Task IsLoading_IsFalse_EvenAfterServiceThrows()
        {
            serviceMock
                .Setup(scheduleService => scheduleService.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("test error"));

            var viewModel = CreateViewModel();
            SetSelectedPharmacist(viewModel, MakePharmacist());

            await viewModel.LoadAsync();

            Assert.False(viewModel.IsLoading);
        }

        [Fact]
        public async Task ErrorMessage_IsEmpty_AfterSuccessfulLoad()
        {
            var viewModel = CreateViewModel();
            SetSelectedPharmacist(viewModel, MakePharmacist());

            await viewModel.LoadAsync();

            Assert.Empty(viewModel.ErrorMessage);
        }

        [Fact]
        public void SelectedPharmacist_Setter_UpdatesPropertyValue()
        {
            var viewModel = CreateViewModel();
            var pharmacist = MakePharmacist(id: 5, name: "Charlie");

            viewModel.SelectedPharmacist = pharmacist;

            Assert.Equal(pharmacist, viewModel.SelectedPharmacist);
        }

        [Fact]
        public void IsDailyView_Setter_SetsIsWeeklyViewToOpposite()
        {
            var viewModel = CreateViewModel();
            SetIsWeeklyView(viewModel, true);

            viewModel.IsDailyView = true;

            Assert.False(viewModel.IsWeeklyView);
            Assert.True(viewModel.IsDailyView);
        }
    }
}
