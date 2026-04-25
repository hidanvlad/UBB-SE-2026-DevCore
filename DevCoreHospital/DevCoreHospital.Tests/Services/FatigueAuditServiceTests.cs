using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class FatigueAuditServiceTests
    {
        private static readonly DateTime WeekStart = new DateTime(2025, 4, 14);

        private readonly Mock<IFatigueAuditRepository> repositoryMock;
        private readonly FatigueAuditService fatigueAuditService;

        public FatigueAuditServiceTests()
        {
            repositoryMock = new Mock<IFatigueAuditRepository>();
            fatigueAuditService = new FatigueAuditService(repositoryMock.Object);
        }

        private void SetupRepo(IReadOnlyList<RosterShift> shifts, IReadOnlyList<StaffProfile> profiles)
        {
            repositoryMock.Setup(repository => repository.GetAllShifts()).Returns(shifts);
            repositoryMock.Setup(repository => repository.GetStaffProfiles()).Returns(profiles);
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

        private static StaffProfile MakeProfile(
            int staffId, string fullName, string role, string specialization,
            bool isAvailable = true, bool isActive = true)
            => new StaffProfile
            {
                StaffId = staffId,
                FullName = fullName,
                Role = role,
                Specialization = specialization,
                IsAvailable = isAvailable,
                IsActive = isActive,
            };

        [Fact]
        public void RunAutoAudit_ReturnsNoViolations_WhenRosterIsEmpty()
        {
            SetupRepo(Array.Empty<RosterShift>(), Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.False(result.HasConflicts);
            Assert.True(result.CanPublish);
            Assert.Empty(result.Violations);
            Assert.Empty(result.Suggestions);
        }

        [Fact]
        public void RunAutoAudit_SetsWeekStart_ToNormalizedMonday()
        {
            SetupRepo(Array.Empty<RosterShift>(), Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.Equal(WeekStart, result.WeekStart);
        }

        [Fact]
        public void RunAutoAudit_NormalizesAnyDayOfWeek_ToMonday()
        {
            var wednesday = new DateTime(2025, 4, 16);
            SetupRepo(Array.Empty<RosterShift>(), Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(wednesday);

            Assert.Equal(WeekStart, result.WeekStart);
        }

        [Fact]
        public void RunAutoAudit_DetectsMaxWeeklyHoursViolation_WhenStaffExceeds60Hours()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            SetupRepo(shifts, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.True(result.HasConflicts);
            Assert.Equal(3, result.Violations.Count);
            Assert.All(result.Violations, violation => Assert.Equal("MAX_60H_PER_WEEK", violation.Rule));
        }

        [Fact]
        public void RunAutoAudit_MaxWeeklyViolationMessage_ContainsTotalHours()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            SetupRepo(shifts, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.All(result.Violations, violation => Assert.Contains("63", violation.Message));
        }

        [Fact]
        public void RunAutoAudit_DoesNotViolate_WhenWeeklyHoursExactly60()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(30)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(30)),
            };
            SetupRepo(shifts, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.DoesNotContain(result.Violations, violation => violation.Rule == "MAX_60H_PER_WEEK");
        }

        [Fact]
        public void RunAutoAudit_DetectsMinRestGapViolation_WhenRestGapBelow12Hours()
        {
            var shift1 = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(8), WeekStart.AddHours(20));
            var shift2 = MakeShift(2, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(22), WeekStart.AddHours(32));
            SetupRepo(new List<RosterShift> { shift1, shift2 }, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.True(result.HasConflicts);
            Assert.Contains(result.Violations, violation => violation.Rule == "MIN_12H_REST" && violation.ShiftId == 2);
        }

        [Fact]
        public void RunAutoAudit_MinRestViolationMessage_ContainsActualGap()
        {
            var shift1 = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(8), WeekStart.AddHours(20));
            var shift2 = MakeShift(2, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(22), WeekStart.AddHours(32));
            SetupRepo(new List<RosterShift> { shift1, shift2 }, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            var restViolation = result.Violations.Single(violation => violation.Rule == "MIN_12H_REST");
            Assert.Contains(2.0.ToString("F1"), restViolation.Message);
        }

        [Fact]
        public void RunAutoAudit_NoRestViolation_WhenGapExactly12Hours()
        {
            var shift1 = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(8), WeekStart.AddHours(20));
            var shift2 = MakeShift(2, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(32), WeekStart.AddHours(40));
            SetupRepo(new List<RosterShift> { shift1, shift2 }, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.DoesNotContain(result.Violations, violation => violation.Rule == "MIN_12H_REST");
        }

        [Fact]
        public void RunAutoAudit_ExcludesCancelledShifts_FromViolationDetection()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21), "CANCELLED"),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21), "CANCELLED"),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21), "CANCELLED"),
            };
            SetupRepo(shifts, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.False(result.HasConflicts);
            Assert.Empty(result.Violations);
        }

        [Fact]
        public void RunAutoAudit_ExcludesShiftsOutsideCurrentWeek()
        {
            var previousWeekShift = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddDays(-7), WeekStart.AddDays(-7).AddHours(50));
            SetupRepo(new List<RosterShift> { previousWeekShift }, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.False(result.HasConflicts);
        }

        [Fact]
        public void RunAutoAudit_BuildsSuggestionWithCandidate_WhenEligibleStaffExists()
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
            SetupRepo(shifts, profiles);

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            var suggestion = result.Suggestions.First(suggestion => suggestion.ShiftId == 1);
            Assert.Equal(2, suggestion.SuggestedStaffId);
            Assert.Equal("Bob", suggestion.SuggestedStaffName);
        }

        [Fact]
        public void RunAutoAudit_SuggestionHasNullCandidate_WhenNoEligibleStaffExists()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            SetupRepo(shifts, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.All(result.Suggestions, suggestion => Assert.Null(suggestion.SuggestedStaffId));
        }

        [Fact]
        public void RunAutoAudit_SuggestionUsesFallback_WhenNoSpecializationMatch()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            var profiles = new List<StaffProfile>
            {
                MakeProfile(2, "Bob", "Doctor", "Neurology"),
            };
            SetupRepo(shifts, profiles);

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            var suggestion = result.Suggestions.First(suggestion => suggestion.SuggestedStaffId.HasValue);
            Assert.Equal(2, suggestion.SuggestedStaffId);
            Assert.Contains("Fallback", suggestion.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RunAutoAudit_ExcludesUnavailableStaff_FromSuggestions()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            var profiles = new List<StaffProfile>
            {
                MakeProfile(2, "Bob", "Doctor", "Cardiology", isAvailable: false),
            };
            SetupRepo(shifts, profiles);

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.All(result.Suggestions, suggestion => Assert.Null(suggestion.SuggestedStaffId));
        }

        [Fact]
        public void RunAutoAudit_ExcludesInactiveStaff_FromCandidatePool()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            var profiles = new List<StaffProfile>
            {
                MakeProfile(2, "Bob", "Doctor", "Cardiology", isActive: false),
            };
            SetupRepo(shifts, profiles);

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.All(result.Suggestions, suggestion => Assert.Null(suggestion.SuggestedStaffId));
        }

        [Fact]
        public void ReassignShift_DelegatesToRepository_AndReturnsItsResult()
        {
            repositoryMock.Setup(repository => repository.UpdateShiftStaffId(10, 20)).Returns(1);

            var result = fatigueAuditService.ReassignShift(10, 20);

            Assert.True(result);
            repositoryMock.Verify(repository => repository.UpdateShiftStaffId(10, 20), Times.Once);
        }

        [Fact]
        public void ReassignShift_ReturnsFalse_WhenRepositoryReturnsFalse()
        {
            repositoryMock.Setup(repository => repository.UpdateShiftStaffId(It.IsAny<int>(), It.IsAny<int>())).Returns(0);

            var result = fatigueAuditService.ReassignShift(5, 7);

            Assert.False(result);
        }

        [Fact]
        public void ReassignShift_ReturnsFalse_WhenIdsAreInvalid()
        {
            var result = fatigueAuditService.ReassignShift(0, -1);

            Assert.False(result);
            repositoryMock.Verify(repository => repository.UpdateShiftStaffId(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new FatigueAuditService(null!));
        }

        [Fact]
        public void RunAutoAudit_ExcludesStaffWithInactiveStatus_FromCandidatePool()
        {
            var shifts = new List<RosterShift>
            {
                MakeShift(1, 1, "Alice", "Doctor", "Cardiology", WeekStart, WeekStart.AddHours(21)),
                MakeShift(2, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(2), WeekStart.AddDays(2).AddHours(21)),
                MakeShift(3, 1, "Alice", "Doctor", "Cardiology", WeekStart.AddDays(4), WeekStart.AddDays(4).AddHours(21)),
            };
            var profiles = new List<StaffProfile>
            {
                new StaffProfile
                {
                    StaffId = 2,
                    FullName = "Bob",
                    Role = "Doctor",
                    Specialization = "Cardiology",
                    IsAvailable = true,
                    IsActive = true,
                    Status = "INACTIVE",
                },
            };
            SetupRepo(shifts, profiles);

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.All(result.Suggestions, suggestion => Assert.Null(suggestion.SuggestedStaffId));
        }

        [Fact]
        public void RunAutoAudit_DoesNotFlagRestViolation_WhenViolatingShiftIsOutsideWeekWindow()
        {
            var shift1 = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddDays(6).AddHours(20), WeekStart.AddDays(7));
            var shift2 = MakeShift(2, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddDays(7).AddHours(2), WeekStart.AddDays(7).AddHours(10));
            SetupRepo(new List<RosterShift> { shift1, shift2 }, Array.Empty<StaffProfile>());

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            Assert.False(result.HasConflicts);
            Assert.DoesNotContain(result.Violations, violation => violation.ShiftId == 2);
        }

        [Fact]
        public void RunAutoAudit_ExcludesCandidate_WhenCandidateHasOverlappingShift()
        {
            var aliceShift1 = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(8), WeekStart.AddHours(20));
            var aliceShift2 = MakeShift(2, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(22), WeekStart.AddHours(32));
            var bobShift = MakeShift(3, 2, "Bob", "Doctor", "Cardiology",
                WeekStart.AddHours(21), WeekStart.AddHours(33));
            var profiles = new List<StaffProfile> { MakeProfile(2, "Bob", "Doctor", "Cardiology") };
            SetupRepo(new List<RosterShift> { aliceShift1, aliceShift2, bobShift }, profiles);

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            var suggestion = result.Suggestions.FirstOrDefault(suggestion => suggestion.ShiftId == 2);
            Assert.NotNull(suggestion);
            Assert.Null(suggestion.SuggestedStaffId);
        }

        [Fact]
        public void RunAutoAudit_ExcludesCandidate_WhenRestGapWithNextShiftIsTooSmall()
        {
            var aliceShift1 = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(8), WeekStart.AddHours(20));
            var aliceShift2 = MakeShift(2, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(22), WeekStart.AddHours(32));
            var bobShift = MakeShift(3, 2, "Bob", "Doctor", "Cardiology",
                WeekStart.AddHours(38), WeekStart.AddHours(46));
            var profiles = new List<StaffProfile> { MakeProfile(2, "Bob", "Doctor", "Cardiology") };
            SetupRepo(new List<RosterShift> { aliceShift1, aliceShift2, bobShift }, profiles);

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            var suggestion = result.Suggestions.FirstOrDefault(suggestion => suggestion.ShiftId == 2);
            Assert.NotNull(suggestion);
            Assert.Null(suggestion.SuggestedStaffId);
        }

        [Fact]
        public void RunAutoAudit_ExcludesCandidate_WhenRestGapWithPreviousShiftIsTooSmall()
        {
            var aliceShift1 = MakeShift(1, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(8), WeekStart.AddHours(20));
            var aliceShift2 = MakeShift(2, 1, "Alice", "Doctor", "Cardiology",
                WeekStart.AddHours(22), WeekStart.AddHours(32));
            var bobShift = MakeShift(3, 2, "Bob", "Doctor", "Cardiology",
                WeekStart.AddHours(8), WeekStart.AddHours(16));
            var profiles = new List<StaffProfile> { MakeProfile(2, "Bob", "Doctor", "Cardiology") };
            SetupRepo(new List<RosterShift> { aliceShift1, aliceShift2, bobShift }, profiles);

            var result = fatigueAuditService.RunAutoAudit(WeekStart);

            var suggestion = result.Suggestions.FirstOrDefault(suggestion => suggestion.ShiftId == 2);
            Assert.NotNull(suggestion);
            Assert.Null(suggestion.SuggestedStaffId);
        }
    }
}
