using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
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

            userMock.Setup(u => u.Role).Returns("Pharmacist");
            userMock.Setup(u => u.UserId).Returns(1);

            serviceMock
                .Setup(s => s.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift>());
        }

        // StaffRepository is a concrete class; pass null since tests never call InitializeAsync
        private PharmacyScheduleViewModel CreateViewModel()
            => new PharmacyScheduleViewModel(userMock.Object, serviceMock.Object, null!);

        // Sets the backing field directly to avoid triggering the fire-and-forget LoadAsync in the setter
        private static void SetSelectedPharmacist(PharmacyScheduleViewModel vm, PharmacyScheduleViewModel.PharmacistOption? pharmacist)
        {
            var field = typeof(PharmacyScheduleViewModel)
                .GetField("selectedPharmacist", BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(vm, pharmacist);
        }

        private static void SetIsWeeklyView(PharmacyScheduleViewModel vm, bool value)
        {
            var field = typeof(PharmacyScheduleViewModel)
                .GetField("isWeeklyView", BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(vm, value);
        }

        private static void SetAnchorDate(PharmacyScheduleViewModel vm, DateTime date)
        {
            var field = typeof(PharmacyScheduleViewModel)
                .GetField("anchorDate", BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(vm, date);
        }

        private static PharmacyScheduleViewModel.PharmacistOption MakePharmacist(int id = 1, string name = "Alice Smith")
            => new PharmacyScheduleViewModel.PharmacistOption { StaffId = id, PharmacistName = name };

        // --- Access denied ---

        [Fact]
        public void IsAccessDenied_IsTrue_WhenRoleIsNotPharmacistOrAdmin()
        {
            userMock.Setup(u => u.Role).Returns("Doctor");
            var vm = CreateViewModel();

            Assert.True(vm.IsAccessDenied);
            Assert.False(vm.IsPharmacist);
        }

        [Fact]
        public void IsPharmacist_IsTrue_WhenRoleIsPharmacist()
        {
            userMock.Setup(u => u.Role).Returns("Pharmacist");
            var vm = CreateViewModel();

            Assert.True(vm.IsPharmacist);
        }

        [Fact]
        public void IsPharmacist_IsTrue_WhenRoleIsAdmin()
        {
            userMock.Setup(u => u.Role).Returns("Admin");
            var vm = CreateViewModel();

            Assert.True(vm.IsPharmacist);
        }

        [Fact]
        public async Task LoadAsync_ClearsShifts_AndDoesNotCallService_WhenAccessDenied()
        {
            userMock.Setup(u => u.Role).Returns("Doctor");
            var vm = CreateViewModel();

            await vm.LoadAsync();

            Assert.Empty(vm.Shifts);
            serviceMock.Verify(
                s => s.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()),
                Times.Never);
        }

        [Fact]
        public async Task LoadAsync_ReturnsEarly_WhenNoPharmacistSelected()
        {
            var vm = CreateViewModel();
            SetSelectedPharmacist(vm, null);

            await vm.LoadAsync();

            Assert.Empty(vm.Shifts);
            serviceMock.Verify(
                s => s.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()),
                Times.Never);
        }

        // --- Pharmacist path: daily ---

        [Fact]
        public async Task LoadAsync_UsesCorrectDailyRange_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 16); // Wednesday
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, false);
            SetSelectedPharmacist(vm, MakePharmacist(id: 1));

            await vm.LoadAsync();

            var expectedStart = anchor.Date;
            var expectedEnd = anchor.Date.AddDays(1);
            serviceMock.Verify(
                s => s.GetShiftsAsync(1, expectedStart, expectedEnd),
                Times.Once);
        }

        [Fact]
        public async Task LoadAsync_UsesCorrectWeeklyRange_WhenIsWeeklyView()
        {
            // Anchor = Wednesday 2025-04-16 → week starts Monday 2025-04-14
            var anchor = new DateTime(2025, 4, 16);
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, true);
            SetSelectedPharmacist(vm, MakePharmacist(id: 1));

            await vm.LoadAsync();

            var expectedStart = new DateTime(2025, 4, 14); // Monday
            var expectedEnd = new DateTime(2025, 4, 21);   // +7 days
            serviceMock.Verify(
                s => s.GetShiftsAsync(1, expectedStart, expectedEnd),
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
                .Setup(s => s.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift> { shift });

            var vm = CreateViewModel();
            SetSelectedPharmacist(vm, MakePharmacist(id: 1));

            await vm.LoadAsync();

            Assert.Single(vm.Shifts);
        }

        [Fact]
        public async Task LoadAsync_SetsErrorMessage_WhenServiceThrows()
        {
            serviceMock
                .Setup(s => s.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("DB error"));

            var vm = CreateViewModel();
            SetSelectedPharmacist(vm, MakePharmacist());

            await vm.LoadAsync();

            Assert.Contains("Failed to load pharmacy schedule", vm.ErrorMessage);
        }

        // --- Computed properties ---

        [Fact]
        public void HeaderSubtitle_ShowsWeekRange_WhenIsWeeklyView()
        {
            var anchor = new DateTime(2025, 4, 16); // Wednesday → week Mon 14 – Sun 20
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, true);

            var subtitle = vm.HeaderSubtitle;

            Assert.Contains("14 Apr 2025", subtitle);
            Assert.Contains("20 Apr 2025", subtitle);
        }

        [Fact]
        public void HeaderSubtitle_ShowsDayName_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 16); // Wednesday
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, false);

            Assert.Contains("Wednesday", vm.HeaderSubtitle);
            Assert.Contains("16 Apr 2025", vm.HeaderSubtitle);
        }

        [Fact]
        public void SelectedDateText_ShowsWeekOf_WhenIsWeeklyView()
        {
            var anchor = new DateTime(2025, 4, 16);
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, true);

            Assert.StartsWith("Week of ", vm.SelectedDateText);
            Assert.Contains("14 Apr 2025", vm.SelectedDateText);
        }

        [Fact]
        public void SelectedDateText_ShowsDayDate_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 16);
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, false);

            Assert.Contains("Wednesday", vm.SelectedDateText);
        }

        [Fact]
        public void IsDailyView_IsOppositeOfIsWeeklyView()
        {
            var vm = CreateViewModel();
            SetIsWeeklyView(vm, true);

            Assert.False(vm.IsDailyView);

            SetIsWeeklyView(vm, false);

            Assert.True(vm.IsDailyView);
        }

        // --- Navigation commands ---

        [Fact]
        public void NextPeriodCommand_AdvancesAnchorByOneWeek_WhenIsWeeklyView()
        {
            var anchor = new DateTime(2025, 4, 14);
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, true);

            vm.NextPeriodCommand.Execute(null);

            Assert.Equal(anchor.AddDays(7), vm.AnchorDate);
        }

        [Fact]
        public void NextPeriodCommand_AdvancesAnchorByOneDay_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 14);
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, false);

            vm.NextPeriodCommand.Execute(null);

            Assert.Equal(anchor.AddDays(1), vm.AnchorDate);
        }

        [Fact]
        public void PreviousPeriodCommand_GoesBackOneWeek_WhenIsWeeklyView()
        {
            var anchor = new DateTime(2025, 4, 14);
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, true);

            vm.PreviousPeriodCommand.Execute(null);

            Assert.Equal(anchor.AddDays(-7), vm.AnchorDate);
        }

        [Fact]
        public void PreviousPeriodCommand_GoesBackOneDay_WhenIsDailyView()
        {
            var anchor = new DateTime(2025, 4, 14);
            var vm = CreateViewModel();
            SetAnchorDate(vm, anchor);
            SetIsWeeklyView(vm, false);

            vm.PreviousPeriodCommand.Execute(null);

            Assert.Equal(anchor.AddDays(-1), vm.AnchorDate);
        }

        [Fact]
        public void ShowDailyCommand_SetsIsDailyViewTrue()
        {
            var vm = CreateViewModel();
            SetIsWeeklyView(vm, true);

            vm.ShowDailyCommand.Execute(null);

            Assert.True(vm.IsDailyView);
            Assert.False(vm.IsWeeklyView);
        }

        [Fact]
        public void ShowWeeklyCommand_SetsIsWeeklyViewTrue()
        {
            var vm = CreateViewModel();
            SetIsWeeklyView(vm, false);

            vm.ShowWeeklyCommand.Execute(null);

            Assert.True(vm.IsWeeklyView);
            Assert.False(vm.IsDailyView);
        }

        [Fact]
        public void TodayCommand_SetsAnchorDateToToday()
        {
            var vm = CreateViewModel();
            // Move anchor away from today
            SetAnchorDate(vm, new DateTime(2020, 1, 1));

            vm.TodayCommand.Execute(null);

            Assert.Equal(DateTime.Today, vm.AnchorDate);
        }

        // --- IsEmpty / IsLoading ---

        [Fact]
        public async Task IsEmpty_IsTrue_WhenNoShiftsLoadedAndNoError()
        {
            serviceMock
                .Setup(s => s.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift>());

            var vm = CreateViewModel();
            SetSelectedPharmacist(vm, MakePharmacist());

            await vm.LoadAsync();

            Assert.True(vm.IsEmpty);
        }

        [Fact]
        public async Task IsEmpty_IsFalse_WhenShiftsAreLoaded()
        {
            var staff = new Doctor { StaffID = 1, FirstName = "T", LastName = "D" };
            var shift = new Shift(1, staff, "X", DateTime.Today, DateTime.Today.AddHours(8), ShiftStatus.SCHEDULED);
            serviceMock
                .Setup(s => s.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Shift> { shift });

            var vm = CreateViewModel();
            SetSelectedPharmacist(vm, MakePharmacist());

            await vm.LoadAsync();

            Assert.False(vm.IsEmpty);
        }

        [Fact]
        public async Task IsLoading_IsFalseAfterLoadCompletes()
        {
            var vm = CreateViewModel();
            SetSelectedPharmacist(vm, MakePharmacist());

            await vm.LoadAsync();

            Assert.False(vm.IsLoading);
        }

        [Fact]
        public async Task IsLoading_IsFalse_EvenAfterServiceThrows()
        {
            serviceMock
                .Setup(s => s.GetShiftsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("test error"));

            var vm = CreateViewModel();
            SetSelectedPharmacist(vm, MakePharmacist());

            await vm.LoadAsync();

            Assert.False(vm.IsLoading);
        }

        [Fact]
        public async Task ErrorMessage_IsEmpty_AfterSuccessfulLoad()
        {
            var vm = CreateViewModel();
            SetSelectedPharmacist(vm, MakePharmacist());

            await vm.LoadAsync();

            Assert.Empty(vm.ErrorMessage);
        }

        [Fact]
        public void SelectedPharmacist_Setter_UpdatesPropertyValue()
        {
            var vm = CreateViewModel();
            var pharmacist = MakePharmacist(id: 5, name: "Charlie");

            vm.SelectedPharmacist = pharmacist;

            Assert.Equal(pharmacist, vm.SelectedPharmacist);
        }

        [Fact]
        public void IsDailyView_Setter_SetsIsWeeklyViewToOpposite()
        {
            var vm = CreateViewModel();
            SetIsWeeklyView(vm, true);

            vm.IsDailyView = true;

            Assert.False(vm.IsWeeklyView);
            Assert.True(vm.IsDailyView);
        }
    }
}
