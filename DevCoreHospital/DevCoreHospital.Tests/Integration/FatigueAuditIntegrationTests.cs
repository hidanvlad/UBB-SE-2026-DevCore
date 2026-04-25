using System;
using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Moq;

namespace DevCoreHospital.Tests.Integration
{
    public class FatigueAuditIntegrationTests
    {
        private static readonly DateTime WeekStart = CurrentWeekMonday();

        private static DateTime CurrentWeekMonday()
        {
            const int daysInWeek = 7;
            var today = DateTime.Today;
            var daysFromMonday = (daysInWeek + (today.DayOfWeek - DayOfWeek.Monday)) % daysInWeek;
            return today.Date.AddDays(-daysFromMonday);
        }

        private readonly Mock<IFatigueAuditRepository> repositoryMock;
        private readonly FatigueAuditService service;

        public FatigueAuditIntegrationTests()
        {
            repositoryMock = new Mock<IFatigueAuditRepository>();
            service = new FatigueAuditService(repositoryMock.Object);
        }

        private FatigueShiftAuditViewModel CreateViewModel() => new FatigueShiftAuditViewModel(service);

        private void SetupDataSource(IReadOnlyList<RosterShift> shifts, IReadOnlyList<StaffProfile> profiles)
        {
            repositoryMock.Setup(r => r.GetAllShifts()).Returns(shifts);
            repositoryMock.Setup(r => r.GetStaffProfiles()).Returns(profiles);
        }

        private static RosterShift MakeShift(
            int id, int staffId, string staffName,
            string role, string specialization,
            DateTime start, DateTime end,
            string? status = null)
            => new RosterShift
            {
                Id = id,
                StaffId = staffId,
                StaffName = staffName,
                Role = role,
                Specialization = specialization,
                Start = start,
                End = end,
                Status = status,
            };

        private static StaffProfile MakeProfile(int staffId, string fullName, string role, string specialization)
            => new StaffProfile
            {
                StaffId = staffId,
                FullName = fullName,
                Role = role,
                Specialization = specialization,
                IsAvailable = true,
                IsActive = true,
            };

        [Fact]
        public void Integration_CleanRoster_ViewModelShowsNoViolationsAndCanPublish()
        {
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
            void HasMaxWeeklyHoursRule(FatigueShiftAuditViewModel.AuditViolationRow violation) => Assert.Equal("MAX_60H_PER_WEEK", violation.Rule);
            Assert.All(vm.Violations, HasMaxWeeklyHoursRule);
        }

        [Fact]
        public void Integration_RestGapViolation_ViewModelReportsMinRestRule()
        {
            var shift1 = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(8), WeekStart.AddHours(20));
            var shift2 = MakeShift(2, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(22), WeekStart.AddHours(32));
            SetupDataSource(new List<RosterShift> { shift1, shift2 }, Array.Empty<StaffProfile>());

            var vm = CreateViewModel();

            Assert.True(vm.HasConflicts);
            bool IsMinRestViolation(FatigueShiftAuditViewModel.AuditViolationRow violation) => violation.Rule == "MIN_12H_REST";
            Assert.Contains(vm.Violations, IsMinRestViolation);
        }

        [Fact]
        public void Integration_CancelledShifts_AreIgnoredByAudit()
        {
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
            repositoryMock.Setup(r => r.UpdateShiftStaffId(It.IsAny<int>(), It.IsAny<int>())).Returns(1);

            var vm = CreateViewModel();
            Assert.True(vm.HasConflicts);

            SetupDataSource(Array.Empty<RosterShift>(), Array.Empty<StaffProfile>());
            var firstSuggestionShiftId = vm.Suggestions[0].ShiftId;

            var result = vm.ApplyReassignment(firstSuggestionShiftId);

            Assert.True(result.isSuccess);
            Assert.False(vm.HasConflicts);
            Assert.True(vm.CanPublish);
        }

        [Fact]
        public void Integration_ApplyReassignment_ReturnsFailure_WhenNoSuggestionExists()
        {
            SetupDataSource(Array.Empty<RosterShift>(), Array.Empty<StaffProfile>());
            var vm = CreateViewModel();

            var result = vm.ApplyReassignment(shiftId: 99);

            Assert.False(result.isSuccess);
        }

    }
}
