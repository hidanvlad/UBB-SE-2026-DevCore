using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Admin;
using Moq;
using Xunit;

namespace DevCoreHospital.Tests.ViewModels
{
    public class AdminShiftViewModelTests
    {
        [Fact]
        public void Constructor_WhenInitialized_LoadsTodayShiftsOrderedByStartTime()
        {
            var today = DateTime.Today;
            var doctor = BuildDoctor(1, "Cardiology");
            var earlyShift = BuildShift(1, doctor, "ER", today.AddHours(8), today.AddHours(10));
            var lateShift = BuildShift(2, doctor, "ER", today.AddHours(12), today.AddHours(14));
            var tomorrowShift = BuildShift(3, doctor, "ER", today.AddDays(1).AddHours(8), today.AddDays(1).AddHours(10));

            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift> { lateShift, tomorrowShift, earlyShift });

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            int GetShiftId(Shift shift) => shift.Id;
            Assert.Equal(new[] { earlyShift.Id, lateShift.Id }, viewModel.Shifts.Select(GetShiftId).ToArray());
        }

        [Fact]
        public void Constructor_WhenInitialized_SetsDailyScheduleTitle()
        {
            var today = DateTime.Today;
            var englishCulture = CultureInfo.GetCultureInfo("en-US");
            var expectedTitle = $"Daily Roster ({today.ToString("dddd, dd MMM yyyy", englishCulture)})";

            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            Assert.Equal(expectedTitle, viewModel.ScheduleTitle);
        }

        [Fact]
        public void IsWeeklyView_WhenSetTrue_LoadAndFilterShiftsDoesNotApplyDateFilter()
        {
            var selectedDate = new DateTime(2030, 5, 14);
            var doctor = BuildDoctor(2, "Neurology");
            var shift1 = BuildShift(11, doctor, "ER", selectedDate.Date.AddHours(8), selectedDate.Date.AddHours(10));
            var shift2 = BuildShift(12, doctor, "ER", selectedDate.Date.AddDays(2).AddHours(8), selectedDate.Date.AddDays(2).AddHours(10));

            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift> { shift2, shift1 });

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.SelectedDate = selectedDate;
            viewModel.IsWeeklyView = true;

            int GetShiftId(Shift shift) => shift.Id;
            Assert.Equal(new[] { shift1.Id, shift2.Id }, viewModel.Shifts.Select(GetShiftId).ToArray());
        }

        [Fact]
        public void IsWeeklyView_WhenSetTrue_SetsWeeklyScheduleTitle()
        {
            var selectedDate = new DateTime(2030, 5, 15); // Wednesday
            var englishCulture = CultureInfo.GetCultureInfo("en-US");
            var startOfWeek = selectedDate.Date.AddDays(-(7 + (selectedDate.DayOfWeek - DayOfWeek.Monday)) % 7);
            var expectedTitle = $"Weekly Roster (Week of {startOfWeek.ToString("dd MMM yyyy", englishCulture)})";

            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.SelectedDate = selectedDate;
            viewModel.IsWeeklyView = true;

            Assert.Equal(expectedTitle, viewModel.ScheduleTitle);
        }

        [Fact]
        public void SelectedDepartment_WhenSetToSpecificDepartment_FiltersShiftsByLocation()
        {
            var selectedDate = new DateTime(2030, 5, 16);
            var doctor = BuildDoctor(3, "Oncology");
            var erShift = BuildShift(21, doctor, "ER", selectedDate.Date.AddHours(8), selectedDate.Date.AddHours(10));
            var pharmacyShift = BuildShift(22, doctor, "Pharmacy", selectedDate.Date.AddHours(11), selectedDate.Date.AddHours(13));

            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift> { erShift, pharmacyShift });

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.SelectedDate = selectedDate;
            viewModel.IsWeeklyView = true;
            viewModel.SelectedDepartment = "ER";

            int GetShiftId(Shift shift) => shift.Id;
            Assert.Equal(new[] { erShift.Id }, viewModel.Shifts.Select(GetShiftId).ToArray());
        }

        [Fact]
        public void FilterSpecializationsAndCertificationsForLocation_WhenCalled_ReplacesCollectionWithServiceValues()
        {
            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift>());
            serviceMock
                .Setup(service => service.GetSpecializationsAndCertificationsForLocation("ER"))
                .Returns(new List<string> { "Cardiology", "Neurology" });

            var viewModel = new AdminShiftViewModel(serviceMock.Object);
            viewModel.SpecializationsAndCertifications.Add("Old Value");

            viewModel.FilterSpecializationsAndCertificationsForLocation("ER");

            Assert.Equal(new[] { "Cardiology", "Neurology" }, viewModel.SpecializationsAndCertifications.ToArray());
        }

        [Fact]
        public void FilterStaffForShift_WhenCalled_ReplacesAvailableStaffWithServiceValues()
        {
            var doctor1 = BuildDoctor(40, "Cardiology");
            var doctor2 = BuildDoctor(41, "Cardiology");

            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift>());
            serviceMock
                .Setup(service => service.GetFilteredStaff("ER", "Cardio"))
                .Returns(new List<IStaff> { doctor1, doctor2 });

            var viewModel = new AdminShiftViewModel(serviceMock.Object);
            viewModel.AvailableStaff.Add(BuildDoctor(99, "Old"));

            viewModel.FilterStaffForShift("ER", "Cardio");

            int GetStaffId(IStaff staff) => staff.StaffID;
            Assert.Equal(new[] { doctor1.StaffID, doctor2.StaffID }, viewModel.AvailableStaff.Select(GetStaffId).ToArray());
        }

        [Fact]
        public void CreateNewShift_WhenNoOverlap_DelegatesToTryAddShift()
        {
            var doctor = BuildDoctor(50, "Cardiology");
            var start = new DateTime(2030, 6, 1, 8, 0, 0);
            var end = new DateTime(2030, 6, 1, 16, 0, 0);

            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift>());
            serviceMock
                .Setup(service => service.TryAddShift(doctor, start, end, "ER"))
                .Returns(true);

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.CreateNewShift(doctor, start, end, "ER");

            serviceMock.Verify(service => service.TryAddShift(doctor, start, end, "ER"), Times.Once);
        }

        [Fact]
        public void CreateNewShift_WhenTryAddShiftReturnsFalse_DoesNotReloadShifts()
        {
            var doctor = BuildDoctor(51, "Cardiology");
            var start = new DateTime(2030, 6, 2, 8, 0, 0);
            var end = new DateTime(2030, 6, 2, 16, 0, 0);

            var serviceMock = new Mock<IShiftManagementService>();
            int getWeeklyCalls = 0;
            List<Shift> GetWeeklyShiftsAndCount()
            {
                getWeeklyCalls++;
                return new List<Shift>();
            }
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(GetWeeklyShiftsAndCount);
            serviceMock
                .Setup(service => service.TryAddShift(doctor, start, end, "ER"))
                .Returns(false);

            var viewModel = new AdminShiftViewModel(serviceMock.Object);
            int callsAfterConstruct = getWeeklyCalls;

            viewModel.CreateNewShift(doctor, start, end, "ER");

            Assert.Equal(callsAfterConstruct, getWeeklyCalls);
        }

        [Fact]
        public void SetShiftActive_WhenCalled_ForwardsShiftIdToService()
        {
            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            int capturedShiftId = -1;
            void CaptureShiftId(int shiftId) { capturedShiftId = shiftId; }
            serviceMock
                .Setup(service => service.SetShiftActive(It.IsAny<int>()))
                .Callback<int>(CaptureShiftId);

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.SetShiftActive(123);

            Assert.Equal(123, capturedShiftId);
        }

        [Fact]
        public void CancelShift_WhenCalled_ForwardsShiftIdToService()
        {
            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            int capturedShiftId = -1;
            void CaptureShiftId(int shiftId) { capturedShiftId = shiftId; }
            serviceMock
                .Setup(service => service.CancelShift(It.IsAny<int>()))
                .Callback<int>(CaptureShiftId);

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.CancelShift(456);

            Assert.Equal(456, capturedShiftId);
        }

        [Fact]
        public void ReassignShift_WhenServiceReturnsTrue_ReloadsShifts()
        {
            var shift = BuildShift(60, BuildDoctor(60, "Cardiology"), "ER", new DateTime(2030, 6, 3, 8, 0, 0), new DateTime(2030, 6, 3, 16, 0, 0));
            var replacement = BuildDoctor(61, "Cardiology");

            var serviceMock = new Mock<IShiftManagementService>();
            int getWeeklyCalls = 0;
            List<Shift> GetWeeklyShiftsAndCount()
            {
                getWeeklyCalls++;
                return new List<Shift>();
            }
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(GetWeeklyShiftsAndCount);
            serviceMock
                .Setup(service => service.ReassignShift(shift, replacement))
                .Returns(true);

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.ReassignShift(shift, replacement);

            Assert.Equal(2, getWeeklyCalls);
        }

        [Fact]
        public void ReassignShift_WhenServiceReturnsFalse_DoesNotReloadShifts()
        {
            var shift = BuildShift(70, BuildDoctor(70, "Cardiology"), "ER", new DateTime(2030, 6, 4, 8, 0, 0), new DateTime(2030, 6, 4, 16, 0, 0));
            var replacement = BuildDoctor(71, "Cardiology");

            var serviceMock = new Mock<IShiftManagementService>();
            int getWeeklyCalls = 0;
            List<Shift> GetWeeklyShiftsAndCount()
            {
                getWeeklyCalls++;
                return new List<Shift>();
            }
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(GetWeeklyShiftsAndCount);
            serviceMock
                .Setup(service => service.ReassignShift(shift, replacement))
                .Returns(false);

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.ReassignShift(shift, replacement);

            Assert.Equal(1, getWeeklyCalls);
        }

        [Fact]
        public void AutoFindReplacement_WhenReplacementsExist_UsesFirstReplacement()
        {
            var shift = BuildShift(80, BuildDoctor(80, "Cardiology"), "ER", new DateTime(2030, 6, 5, 8, 0, 0), new DateTime(2030, 6, 5, 16, 0, 0));
            var firstReplacement = BuildDoctor(81, "Cardiology");
            var secondReplacement = BuildDoctor(82, "Cardiology");

            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift>());
            serviceMock
                .Setup(service => service.FindStaffReplacements(shift))
                .Returns(new List<IStaff> { firstReplacement, secondReplacement });

            int capturedReplacementId = -1;
            void CaptureReplacementId(Shift replacedShift, IStaff staff) { capturedReplacementId = staff.StaffID; }
            serviceMock
                .Setup(service => service.ReassignShift(shift, It.IsAny<IStaff>()))
                .Returns(true)
                .Callback<Shift, IStaff>(CaptureReplacementId);

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.AutoFindReplacement(shift);

            Assert.Equal(firstReplacement.StaffID, capturedReplacementId);
        }

        [Fact]
        public void AutoFindReplacement_WhenNoReplacementsExist_DoesNotReassign()
        {
            var shift = BuildShift(90, BuildDoctor(90, "Cardiology"), "ER", new DateTime(2030, 6, 6, 8, 0, 0), new DateTime(2030, 6, 6, 16, 0, 0));

            var serviceMock = new Mock<IShiftManagementService>();
            serviceMock
                .Setup(service => service.GetWeeklyShifts(It.IsAny<DateTime>()))
                .Returns(new List<Shift>());
            serviceMock
                .Setup(service => service.FindStaffReplacements(shift))
                .Returns(new List<IStaff>());

            int reassignCalls = 0;
            void CountReassignCall() { reassignCalls++; }
            serviceMock
                .Setup(service => service.ReassignShift(It.IsAny<Shift>(), It.IsAny<IStaff>()))
                .Callback(CountReassignCall);

            var viewModel = new AdminShiftViewModel(serviceMock.Object);

            viewModel.AutoFindReplacement(shift);

            Assert.Equal(0, reassignCalls);
        }

        private static Doctor BuildDoctor(int staffId, string specialization)
            => new Doctor(staffId, "John", "Doe", "john.doe@example.com", string.Empty, false, specialization, "LIC-1", DoctorStatus.OFF_DUTY, 5);

        private static Shift BuildShift(int id, IStaff staff, string location, DateTime start, DateTime end)
            => new Shift(id, staff, location, start, end, ShiftStatus.SCHEDULED);
    }
}
