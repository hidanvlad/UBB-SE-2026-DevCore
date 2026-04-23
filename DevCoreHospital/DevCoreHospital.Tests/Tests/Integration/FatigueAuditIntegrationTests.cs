using System;
using System.Collections.Generic;
using DevCoreHospital.Data;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Moq;

namespace DevCoreHospital.Tests.Integration
{
    /// <summary>
    /// Integration tests for the full chain:
    /// IFatigueShiftDataSource (mocked) → FatigueAuditRepository → FatigueAuditService → FatigueShiftAuditViewModel
    /// </summary>
    public class FatigueAuditIntegrationTests
    {
        // Anchor is always the current week's Monday so shifts match the ViewModel's initial SelectedWeekStart.
        private static readonly DateTime WeekStart = CurrentWeekMonday();

        private static DateTime CurrentWeekMonday()
        {
            const int daysInWeek = 7;
            var today = DateTime.Today;
            var daysFromMonday = (daysInWeek + (today.DayOfWeek - DayOfWeek.Monday)) % daysInWeek;
            return today.Date.AddDays(-daysFromMonday);
        }

        private readonly Mock<IFatigueShiftDataSource> dataSourceMock;
        private readonly FatigueAuditRepository repository;
        private readonly FatigueAuditService service;

        public FatigueAuditIntegrationTests()
        {
            dataSourceMock = new Mock<IFatigueShiftDataSource>();
            repository = new FatigueAuditRepository(dataSourceMock.Object);
            service = new FatigueAuditService(repository);
        }

        private FatigueShiftAuditViewModel CreateViewModel() => new FatigueShiftAuditViewModel(service);

        private void SetupDataSource(IReadOnlyList<RosterShift> shifts, IReadOnlyList<StaffProfile> profiles)
        {
            dataSourceMock.Setup(d => d.GetAllShifts()).Returns(shifts);
            dataSourceMock.Setup(d => d.GetStaffProfiles()).Returns(profiles);
        }

        private static RosterShift MakeShift(
            int id, int staffId, string staffName,
            string role, string spec,
            DateTime start, DateTime end,
            string? status = null)
            => new RosterShift
            {
                Id = id,
                StaffId = staffId,
                StaffName = staffName,
                Role = role,
                Specialization = spec,
                Start = start,
                End = end,
                Status = status,
            };

        private static StaffProfile MakeProfile(int staffId, string fullName, string role, string spec)
            => new StaffProfile
            {
                StaffId = staffId,
                FullName = fullName,
                Role = role,
                Specialization = spec,
                IsAvailable = true,
                IsActive = true,
            };

        // --- Full audit flow ---

        [Fact]
        public void Integration_CleanRoster_ViewModelShowsNoViolationsAndCanPublish()
        {
            // Two shifts within 60 h limit, with adequate rest gap
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddHours(8), WeekStart.AddHours(20)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(8)),
            };
            SetupDataSource(shifts, Array.Empty<StaffProfile>());

            var vm = CreateViewModel();

            Assert.False(vm.HasConflicts);
            Assert.True(vm.CanPublish);
            Assert.Empty(vm.Violations);
        }

        [Fact]
        public void Integration_WeeklyHoursExceeded_ViewModelShowsViolationsAndBlocksPublish()
        {
            // 3 × 21 h = 63 h > 60 h
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            SetupDataSource(shifts, Array.Empty<StaffProfile>());

            var vm = CreateViewModel();

            Assert.True(vm.HasConflicts);
            Assert.False(vm.CanPublish);
            Assert.NotEmpty(vm.Violations);
            Assert.All(vm.Violations, v => Assert.Equal("MAX_60H_PER_WEEK", v.Rule));
        }

        [Fact]
        public void Integration_RestGapViolation_ViewModelReportsMinRestRule()
        {
            // 2 h gap between consecutive shifts (< 12 h minimum)
            var shift1 = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(8), WeekStart.AddHours(20));
            var shift2 = MakeShift(2, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(22), WeekStart.AddHours(32));
            SetupDataSource(new List<RosterShift> { shift1, shift2 }, Array.Empty<StaffProfile>());

            var vm = CreateViewModel();

            Assert.True(vm.HasConflicts);
            Assert.Contains(vm.Violations, v => v.Rule == "MIN_12H_REST");
        }

        [Fact]
        public void Integration_CancelledShifts_AreIgnoredByAudit()
        {
            // Shift count would exceed 60 h but all are cancelled
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21), "CANCELLED"),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21), "CANCELLED"),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21), "CANCELLED"),
            };
            SetupDataSource(shifts, Array.Empty<StaffProfile>());

            var vm = CreateViewModel();

            Assert.False(vm.HasConflicts);
            Assert.True(vm.CanPublish);
        }

        [Fact]
        public void Integration_SuggestionsArePopulated_WhenEligibleCandidateExists()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            var profiles = new List<StaffProfile>
            {
                MakeProfile(2, "Bob", "Doctor", "Cardiology"),
            };
            SetupDataSource(shifts, profiles);

            var vm = CreateViewModel();

            Assert.NotEmpty(vm.Suggestions);
            var suggestion = vm.Suggestions[0];
            Assert.NotNull(suggestion.SuggestedStaffId);
        }

        // --- ApplyReassignment triggers re-audit ---

        [Fact]
        public void Integration_ApplyReassignment_TriggersReauditAndUpdatesViewModel()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            var profiles = new List<StaffProfile>
            {
                MakeProfile(2, "Bob", "Doctor", "Cardiology"),
            };
            SetupDataSource(shifts, profiles);
            // After reassignment, the data source returns a clean roster
            dataSourceMock.Setup(d => d.ReassignShift(It.IsAny<int>(), It.IsAny<int>())).Returns(true);

            var vm = CreateViewModel();
            Assert.True(vm.HasConflicts);

            // After the re-audit triggered by ApplyReassignment the data source still returns the
            // same overloaded shifts (we simulate a clean slate by resetting the mock)
            SetupDataSource(Array.Empty<RosterShift>(), Array.Empty<StaffProfile>());
            var firstSuggestionShiftId = vm.Suggestions[0].ShiftId;

            var result = vm.ApplyReassignment(firstSuggestionShiftId);

            Assert.True(result.isSuccess);
            // The re-audit ran on the now-empty roster → no conflicts
            Assert.False(vm.HasConflicts);
            Assert.True(vm.CanPublish);
        }

        [Fact]
        public void Integration_ApplyReassignment_ReturnsFailure_WhenNoSuggestionExists()
        {
            // Clean roster → no violations → no suggestions
            SetupDataSource(Array.Empty<RosterShift>(), Array.Empty<StaffProfile>());
            var vm = CreateViewModel();

            var result = vm.ApplyReassignment(shiftId: 99);

            Assert.False(result.isSuccess);
        }

        // --- Repository layer validation ---

        [Fact]
        public void Integration_Repository_DelegatesToDataSource_ForGetAllShifts()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(8)),
            };
            dataSourceMock.Setup(d => d.GetAllShifts()).Returns(shifts);
            dataSourceMock.Setup(d => d.GetStaffProfiles()).Returns(Array.Empty<StaffProfile>());

            var repoResult = repository.GetAllShifts();

            Assert.Single(repoResult);
            Assert.Equal(1, repoResult[0].Id);
            dataSourceMock.Verify(d => d.GetAllShifts(), Times.AtLeastOnce);
        }

        [Fact]
        public void Integration_Repository_DelegatesToDataSource_ForReassignShift()
        {
            dataSourceMock.Setup(d => d.ReassignShift(5, 10)).Returns(true);

            var result = repository.ReassignShift(5, 10);

            Assert.True(result);
            dataSourceMock.Verify(d => d.ReassignShift(5, 10), Times.Once);
        }
    }
}
