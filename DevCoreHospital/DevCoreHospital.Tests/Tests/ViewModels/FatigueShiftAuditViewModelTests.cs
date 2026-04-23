using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Moq;

namespace DevCoreHospital.Tests.ViewModels
{
    public class FatigueShiftAuditViewModelTests
    {
        private readonly Mock<IFatigueAuditService> auditServiceMock;

        public FatigueShiftAuditViewModelTests()
        {
            auditServiceMock = new Mock<IFatigueAuditService>();
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
        }

        private FatigueShiftAuditViewModel CreateViewModel()
            => new FatigueShiftAuditViewModel(auditServiceMock.Object);

        private static AutoAuditResult CleanResult(DateTime? weekStart = null) => new AutoAuditResult
        {
            WeekStart = weekStart ?? DateTime.Today,
            HasConflicts = false,
            Summary = "No conflicts found. Roster can be published.",
            Violations = Array.Empty<AuditViolation>(),
            Suggestions = Array.Empty<AutoSuggestRecommendation>(),
        };

        private static AutoAuditResult ResultWithViolations(params AuditViolation[] violations)
            => new AutoAuditResult
            {
                WeekStart = DateTime.Today,
                HasConflicts = violations.Length > 0,
                Summary = $"Found {violations.Length} conflict(s).",
                Violations = violations,
                Suggestions = Array.Empty<AutoSuggestRecommendation>(),
            };

        private static AutoAuditResult ResultWithViolationsAndSuggestions(
            IReadOnlyList<AuditViolation> violations,
            IReadOnlyList<AutoSuggestRecommendation> suggestions)
            => new AutoAuditResult
            {
                WeekStart = DateTime.Today,
                HasConflicts = violations.Count > 0,
                Summary = $"Found {violations.Count} conflict(s).",
                Violations = violations,
                Suggestions = suggestions,
            };

        private static AuditViolation MakeViolation(int shiftId = 1, string rule = "MAX_60H_PER_WEEK")
            => new AuditViolation
            {
                ShiftId = shiftId,
                StaffId = 10,
                StaffName = "Alice",
                ShiftStart = DateTime.Today.AddHours(8),
                ShiftEnd = DateTime.Today.AddHours(20),
                Rule = rule,
                Message = "Test violation message.",
            };

        private static AutoSuggestRecommendation MakeSuggestion(int shiftId, int? suggestedStaffId, string suggestedName = "Bob")
            => new AutoSuggestRecommendation
            {
                ShiftId = shiftId,
                OriginalStaffId = 10,
                OriginalStaffName = "Alice",
                SuggestedStaffId = suggestedStaffId,
                SuggestedStaffName = suggestedStaffId.HasValue ? suggestedName : string.Empty,
                Reason = suggestedStaffId.HasValue ? "Lowest monthly load." : "No valid replacement found.",
            };

        [Fact]
        public void Constructor_CallsRunAutoAudit_OnCreation()
        {
            CreateViewModel();

            auditServiceMock.Verify(s => s.RunAutoAudit(It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public void RunAutoAudit_PopulatesViolations_WhenServiceReturnsViolations()
        {
            var violation = MakeViolation(shiftId: 1);
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(violation));
            var vm = CreateViewModel();

            Assert.Single(vm.Violations);
            Assert.Equal(1, vm.Violations[0].ShiftId);
            Assert.Equal("Alice", vm.Violations[0].Staff);
            Assert.Equal("MAX_60H_PER_WEEK", vm.Violations[0].Rule);
        }

        [Fact]
        public void RunAutoAudit_ClearsViolations_BeforePopulating()
        {
            auditServiceMock
                .SetupSequence(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation(1), MakeViolation(2)))
                .Returns(CleanResult());
            var vm = CreateViewModel();

            vm.RunAutoAudit();

            Assert.Empty(vm.Violations);
        }

        [Fact]
        public void RunAutoAudit_PopulatesSuggestions_WhenServiceReturnsSuggestions()
        {
            var violation = MakeViolation(shiftId: 5);
            var suggestion = MakeSuggestion(shiftId: 5, suggestedStaffId: 99);
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            var vm = CreateViewModel();

            Assert.Single(vm.Suggestions);
            Assert.Equal(5, vm.Suggestions[0].ShiftId);
            Assert.Equal(99, vm.Suggestions[0].SuggestedStaffId);
        }

        [Fact]
        public void RunAutoAudit_SetsCanPublishTrue_WhenNoConflicts()
        {
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var vm = CreateViewModel();

            Assert.True(vm.CanPublish);
        }

        [Fact]
        public void RunAutoAudit_SetsCanPublishFalse_WhenConflictsExist()
        {
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()));
            var vm = CreateViewModel();

            Assert.False(vm.CanPublish);
        }

        [Fact]
        public void HasConflicts_IsFalse_WhenNoViolations()
        {
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var vm = CreateViewModel();

            Assert.False(vm.HasConflicts);
        }

        [Fact]
        public void HasConflicts_IsTrue_WhenViolationsExist()
        {
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()));
            var vm = CreateViewModel();

            Assert.True(vm.HasConflicts);
        }

        [Fact]
        public void RunAutoAudit_SetsStatusMessage_FromServiceSummary()
        {
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var vm = CreateViewModel();

            Assert.Equal("No conflicts found. Roster can be published.", vm.StatusMessage);
        }

        [Fact]
        public void WeekLabel_StartsWithWeekOf()
        {
            var vm = CreateViewModel();

            Assert.StartsWith("Week of ", vm.WeekLabel);
        }

        [Fact]
        public void WeekLabel_FormatsDateInEnglish()
        {
            var vm = CreateViewModel();

            // Expected format: "Week of dd MMM yyyy" in en-US
            var mondayOfCurrentWeek = vm.SelectedWeekStart.Date;
            var expected = $"Week of {mondayOfCurrentWeek.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-US"))}";

            Assert.Equal(expected, vm.WeekLabel);
        }

        [Fact]
        public void SelectedWeekStart_Setter_NormalizesToMonday()
        {
            var vm = CreateViewModel();
            var wednesday = new DateTimeOffset(new DateTime(2025, 4, 16)); // Wednesday

            vm.SelectedWeekStart = wednesday;

            Assert.Equal(DayOfWeek.Monday, vm.SelectedWeekStart.DayOfWeek);
            Assert.Equal(new DateTime(2025, 4, 14), vm.SelectedWeekStart.Date); // Monday
        }

        [Fact]
        public void SelectedWeekStart_Setter_RaisesWeekLabelPropertyChanged()
        {
            var vm = CreateViewModel();
            var raisedProperties = new List<string>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is not null)
                    raisedProperties.Add(e.PropertyName);
            };
            var wednesday = new DateTimeOffset(new DateTime(2025, 4, 16));

            vm.SelectedWeekStart = wednesday;

            Assert.Contains(nameof(vm.WeekLabel), raisedProperties);
        }

        [Fact]
        public void CanPublish_Change_RaisesPropertyChangedForPublishStatus()
        {
            // Start with violations (CanPublish = false)
            auditServiceMock
                .SetupSequence(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()))
                .Returns(CleanResult());

            var vm = CreateViewModel();
            var raisedProperties = new List<string>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is not null)
                    raisedProperties.Add(e.PropertyName);
            };

            vm.RunAutoAudit(); // second call returns clean result → CanPublish flips to true

            Assert.Contains(nameof(vm.PublishStatus), raisedProperties);
            Assert.Contains(nameof(vm.PublishStatusDescription), raisedProperties);
        }

        [Fact]
        public void ApplyReassignment_ReturnsFailure_WhenNoSuggestionExistsForShift()
        {
            var vm = CreateViewModel(); // no suggestions

            var result = vm.ApplyReassignment(shiftId: 999);

            Assert.False(result.isSuccess);
            Assert.Equal("Invalid Reassignment", result.title);
        }

        [Fact]
        public void ApplyReassignment_ReturnsFailure_WhenSuggestionHasNoCandidate()
        {
            var violation = MakeViolation(shiftId: 7);
            var suggestion = MakeSuggestion(shiftId: 7, suggestedStaffId: null); // no candidate
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));

            var vm = CreateViewModel();
            var result = vm.ApplyReassignment(shiftId: 7);

            Assert.False(result.isSuccess);
            Assert.Equal("Invalid Reassignment", result.title);
            auditServiceMock.Verify(s => s.ReassignShift(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void ApplyReassignment_ReturnsSuccess_WhenReassignmentSucceeds()
        {
            var violation = MakeViolation(shiftId: 3);
            var suggestion = MakeSuggestion(shiftId: 3, suggestedStaffId: 50, suggestedName: "Bob");
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            auditServiceMock.Setup(s => s.ReassignShift(3, 50)).Returns(true);
            var vm = CreateViewModel();

            var result = vm.ApplyReassignment(shiftId: 3);

            Assert.True(result.isSuccess);
            Assert.Equal("Reassignment Applied", result.title);
            Assert.Contains("Bob", result.message);
        }

        [Fact]
        public void ApplyReassignment_ReturnsFailure_WhenServiceReturnsFalse()
        {
            var violation = MakeViolation(shiftId: 3);
            var suggestion = MakeSuggestion(shiftId: 3, suggestedStaffId: 50);
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            auditServiceMock.Setup(s => s.ReassignShift(3, 50)).Returns(false);
            var vm = CreateViewModel();

            var result = vm.ApplyReassignment(shiftId: 3);

            Assert.False(result.isSuccess);
            Assert.Equal("Reassignment Failed", result.title);
        }

        [Fact]
        public void ApplyReassignment_CallsRunAutoAudit_AfterSuccessfulReassignment()
        {
            var violation = MakeViolation(shiftId: 3);
            var suggestion = MakeSuggestion(shiftId: 3, suggestedStaffId: 50);
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            auditServiceMock.Setup(s => s.ReassignShift(3, 50)).Returns(true);
            var vm = CreateViewModel();

            vm.ApplyReassignment(shiftId: 3);

            // Once in constructor + once after successful reassignment
            auditServiceMock.Verify(s => s.RunAutoAudit(It.IsAny<DateTime>()), Times.Exactly(2));
        }

        [Fact]
        public void ApplyReassignment_DoesNotCallRunAutoAudit_WhenServiceFails()
        {
            var violation = MakeViolation(shiftId: 3);
            var suggestion = MakeSuggestion(shiftId: 3, suggestedStaffId: 50);
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            auditServiceMock.Setup(s => s.ReassignShift(3, 50)).Returns(false);
            var vm = CreateViewModel();

            vm.ApplyReassignment(shiftId: 3);

            // Only the constructor call
            auditServiceMock.Verify(s => s.RunAutoAudit(It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public void PublishStatus_IsReady_WhenCanPublish()
        {
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var vm = CreateViewModel();

            Assert.Equal("Publish status: READY", vm.PublishStatus);
        }

        [Fact]
        public void PublishStatus_IsBlocked_WhenCannotPublish()
        {
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()));
            var vm = CreateViewModel();

            Assert.Equal("Publish status: BLOCKED", vm.PublishStatus);
        }

        [Fact]
        public void PublishStatusDescription_IsPositive_WhenCanPublish()
        {
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var vm = CreateViewModel();

            Assert.Contains("No violations", vm.PublishStatusDescription);
        }

        [Fact]
        public void PublishStatusDescription_IsNegative_WhenCannotPublish()
        {
            auditServiceMock
                .Setup(s => s.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()));
            var vm = CreateViewModel();

            Assert.Contains("cannot be published", vm.PublishStatusDescription);
        }
    }
}
