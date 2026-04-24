using System;
using DevCoreHospital.Models;
using DevCoreHospital.ViewModels.Pharmacy;

namespace DevCoreHospital.Tests.ViewModels
{
    public class PharmacyShiftItemViewModelTests
    {
        private static readonly DateTime DefaultStart = new DateTime(2025, 6, 15, 8, 0, 0);
        private static readonly DateTime DefaultEnd = new DateTime(2025, 6, 15, 16, 30, 0);

        private static PharmacyShiftItemViewModel BuildViewModel(
            string location = "Ward A",
            DateTime? start = null,
            DateTime? end = null,
            ShiftStatus status = ShiftStatus.SCHEDULED)
        {
            var staff = new Doctor(1, "First", "Last", string.Empty, string.Empty, true, "General", "LIC-1", DoctorStatus.AVAILABLE, 1);
            var shift = new Shift(1, staff, location, start ?? DefaultStart, end ?? DefaultEnd, status);
            return new PharmacyShiftItemViewModel(shift);
        }

        [Fact]
        public void Constructor_SetsRotationAssignment_FromShiftLocation()
        {
            var viewModel = BuildViewModel(location: "ICU");

            Assert.Equal("ICU", viewModel.RotationAssignment);
        }

        [Fact]
        public void Constructor_SetsShiftStartTime_FromShiftStartTime()
        {
            var viewModel = BuildViewModel();

            Assert.Equal(DefaultStart, viewModel.ShiftStartTime);
        }

        [Fact]
        public void Constructor_SetsShiftEndTime_FromShiftEndTime()
        {
            var viewModel = BuildViewModel();

            Assert.Equal(DefaultEnd, viewModel.ShiftEndTime);
        }

        [Fact]
        public void ShiftStartTimeText_ReturnsHourMinuteFormat()
        {
            var viewModel = BuildViewModel(start: new DateTime(2025, 6, 15, 9, 30, 0));

            Assert.Equal("09:30", viewModel.ShiftStartTimeText);
        }

        [Fact]
        public void ShiftEndTimeText_ReturnsHourMinuteFormat_WhenEndTimeIsSet()
        {
            var viewModel = BuildViewModel(end: new DateTime(2025, 6, 15, 17, 45, 0));

            Assert.Equal("17:45", viewModel.ShiftEndTimeText);
        }

        [Fact]
        public void DayLabel_ReturnsEnglishFormattedDate()
        {
            var viewModel = BuildViewModel(start: new DateTime(2025, 6, 15, 8, 0, 0));

            Assert.Equal("Sun, 15 Jun 2025", viewModel.DayLabel);
        }

        [Fact]
        public void DurationText_ReturnsCorrectHoursAndMinutes()
        {
            var viewModel = BuildViewModel(
                start: new DateTime(2025, 6, 15, 8, 0, 0),
                end: new DateTime(2025, 6, 15, 10, 30, 0));

            Assert.Equal("2h 30m", viewModel.DurationText);
        }

        [Fact]
        public void DurationText_ReturnsZeroHours_WhenShiftIsUnderOneHour()
        {
            var viewModel = BuildViewModel(
                start: new DateTime(2025, 6, 15, 8, 0, 0),
                end: new DateTime(2025, 6, 15, 8, 45, 0));

            Assert.Equal("0h 45m", viewModel.DurationText);
        }

        [Fact]
        public void DurationText_ReturnsEightHours_ForStandardShift()
        {
            var viewModel = BuildViewModel(
                start: new DateTime(2025, 6, 15, 8, 0, 0),
                end: new DateTime(2025, 6, 15, 16, 0, 0));

            Assert.Equal("8h 0m", viewModel.DurationText);
        }

        [Fact]
        public void StatusDisplay_ReturnsScheduled_WhenStatusIsScheduled()
        {
            var viewModel = BuildViewModel(status: ShiftStatus.SCHEDULED);

            Assert.Equal("Scheduled", viewModel.StatusDisplay);
        }

        [Fact]
        public void StatusDisplay_ReturnsActive_WhenStatusIsActive()
        {
            var viewModel = BuildViewModel(status: ShiftStatus.ACTIVE);

            Assert.Equal("Active", viewModel.StatusDisplay);
        }

        [Fact]
        public void StatusDisplay_ReturnsCompleted_WhenStatusIsCompleted()
        {
            var viewModel = BuildViewModel(status: ShiftStatus.COMPLETED);

            Assert.Equal("Completed", viewModel.StatusDisplay);
        }

        [Fact]
        public void StatusDisplay_ReturnsCancelled_WhenStatusIsCancelled()
        {
            var viewModel = BuildViewModel(status: ShiftStatus.CANCELLED);

            Assert.Equal("Cancelled", viewModel.StatusDisplay);
        }

        [Fact]
        public void TimeRangeDetail_ContainsStartTimeText()
        {
            var viewModel = BuildViewModel(
                start: new DateTime(2025, 6, 15, 8, 0, 0),
                end: new DateTime(2025, 6, 15, 16, 0, 0));

            Assert.Contains("08:00", viewModel.TimeRangeDetail);
        }

        [Fact]
        public void TimeRangeDetail_ContainsEndTimeText()
        {
            var viewModel = BuildViewModel(
                start: new DateTime(2025, 6, 15, 8, 0, 0),
                end: new DateTime(2025, 6, 15, 16, 0, 0));

            Assert.Contains("16:00", viewModel.TimeRangeDetail);
        }

        [Fact]
        public void TimeRangeDetail_ContainsDurationText()
        {
            var viewModel = BuildViewModel(
                start: new DateTime(2025, 6, 15, 8, 0, 0),
                end: new DateTime(2025, 6, 15, 16, 0, 0));

            Assert.Contains("8h 0m", viewModel.TimeRangeDetail);
        }
    }
}
