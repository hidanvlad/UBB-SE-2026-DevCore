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
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
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

            auditServiceMock.Verify(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()), Times.Once);
        }

        private FatigueShiftAuditViewModel CreateViewModelWithSingleViolation()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation(shiftId: 1)));
            return CreateViewModel();
        }

        [Fact]
        public void RunAutoAudit_WhenServiceReturnsViolations_PopulatesSingleViolation()
        {
            Assert.Single(CreateViewModelWithSingleViolation().Violations);
        }

        [Fact]
        public void RunAutoAudit_WhenServiceReturnsViolations_MapsShiftId()
        {
            Assert.Equal(1, CreateViewModelWithSingleViolation().Violations[0].ShiftId);
        }

        [Fact]
        public void RunAutoAudit_WhenServiceReturnsViolations_MapsStaffName()
        {
            Assert.Equal("Alice", CreateViewModelWithSingleViolation().Violations[0].Staff);
        }

        [Fact]
        public void RunAutoAudit_WhenServiceReturnsViolations_MapsRule()
        {
            Assert.Equal("MAX_60H_PER_WEEK", CreateViewModelWithSingleViolation().Violations[0].Rule);
        }

        [Fact]
        public void RunAutoAudit_ClearsViolations_BeforePopulating()
        {
            auditServiceMock
                .SetupSequence(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation(1), MakeViolation(2)))
                .Returns(CleanResult());
            var viewModel = CreateViewModel();

            viewModel.RunAutoAudit();

            Assert.Empty(viewModel.Violations);
        }

        private FatigueShiftAuditViewModel CreateViewModelWithSingleSuggestion()
        {
            var violation = MakeViolation(shiftId: 5);
            var suggestion = MakeSuggestion(shiftId: 5, suggestedStaffId: 99);
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            return CreateViewModel();
        }

        [Fact]
        public void RunAutoAudit_WhenServiceReturnsSuggestions_PopulatesSingleSuggestion()
        {
            Assert.Single(CreateViewModelWithSingleSuggestion().Suggestions);
        }

        [Fact]
        public void RunAutoAudit_WhenServiceReturnsSuggestions_MapsShiftId()
        {
            Assert.Equal(5, CreateViewModelWithSingleSuggestion().Suggestions[0].ShiftId);
        }

        [Fact]
        public void RunAutoAudit_WhenServiceReturnsSuggestions_MapsSuggestedStaffId()
        {
            Assert.Equal(99, CreateViewModelWithSingleSuggestion().Suggestions[0].SuggestedStaffId);
        }

        [Fact]
        public void RunAutoAudit_SetsCanPublishTrue_WhenNoConflicts()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var viewModel = CreateViewModel();

            Assert.True(viewModel.CanPublish);
        }

        [Fact]
        public void RunAutoAudit_SetsCanPublishFalse_WhenConflictsExist()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()));
            var viewModel = CreateViewModel();

            Assert.False(viewModel.CanPublish);
        }

        [Fact]
        public void HasConflicts_IsFalse_WhenNoViolations()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var viewModel = CreateViewModel();

            Assert.False(viewModel.HasConflicts);
        }

        [Fact]
        public void HasConflicts_IsTrue_WhenViolationsExist()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()));
            var viewModel = CreateViewModel();

            Assert.True(viewModel.HasConflicts);
        }

        [Fact]
        public void RunAutoAudit_SetsStatusMessage_FromServiceSummary()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var viewModel = CreateViewModel();

            Assert.Equal("No conflicts found. Roster can be published.", viewModel.StatusMessage);
        }

        [Fact]
        public void WeekLabel_StartsWithWeekOf()
        {
            var viewModel = CreateViewModel();

            Assert.StartsWith("Week of ", viewModel.WeekLabel);
        }

        [Fact]
        public void WeekLabel_FormatsDateInEnglish()
        {
            var viewModel = CreateViewModel();

            var mondayOfCurrentWeek = viewModel.SelectedWeekStart.Date;
            var expected = $"Week of {mondayOfCurrentWeek.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-US"))}";

            Assert.Equal(expected, viewModel.WeekLabel);
        }

        [Fact]
        public void SelectedWeekStart_Setter_NormalizesToMonday()
        {
            var viewModel = CreateViewModel();
            var wednesday = new DateTimeOffset(new DateTime(2025, 4, 16)); // Wednesday

            viewModel.SelectedWeekStart = wednesday;

            Assert.Equal(DayOfWeek.Monday, viewModel.SelectedWeekStart.DayOfWeek);
            Assert.Equal(new DateTime(2025, 4, 14), viewModel.SelectedWeekStart.Date); // Monday
        }

        [Fact]
        public void SelectedWeekStart_Setter_RaisesWeekLabelPropertyChanged()
        {
            var viewModel = CreateViewModel();
            var raisedProperties = new List<string>();
            void OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) => raisedProperties.Add(eventArgs.PropertyName ?? string.Empty);
            viewModel.PropertyChanged += OnPropertyChanged;
            var wednesday = new DateTimeOffset(new DateTime(2025, 4, 16));

            viewModel.SelectedWeekStart = wednesday;

            Assert.Contains(nameof(viewModel.WeekLabel), raisedProperties);
        }

        [Fact]
        public void CanPublish_Change_RaisesPropertyChangedForPublishStatus()
        {
            auditServiceMock
                .SetupSequence(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()))
                .Returns(CleanResult());

            var viewModel = CreateViewModel();
            var raisedProperties = new List<string>();
            void OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) => raisedProperties.Add(eventArgs.PropertyName ?? string.Empty);
            viewModel.PropertyChanged += OnPropertyChanged;

            viewModel.RunAutoAudit();

            Assert.Contains(nameof(viewModel.PublishStatus), raisedProperties);
            Assert.Contains(nameof(viewModel.PublishStatusDescription), raisedProperties);
        }

        [Fact]
        public void ApplyReassignment_ReturnsFailure_WhenNoSuggestionExistsForShift()
        {
            var viewModel = CreateViewModel(); // no suggestions

            var reassignment = viewModel.ApplyReassignment(shiftId: 999);

            Assert.False(reassignment.isSuccess);
            Assert.Equal("Invalid Reassignment", reassignment.title);
        }

        [Fact]
        public void ApplyReassignment_ReturnsFailure_WhenSuggestionHasNoCandidate()
        {
            var violation = MakeViolation(shiftId: 7);
            var suggestion = MakeSuggestion(shiftId: 7, suggestedStaffId: null); // no candidate
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));

            var viewModel = CreateViewModel();
            var reassignment = viewModel.ApplyReassignment(shiftId: 7);

            Assert.False(reassignment.isSuccess);
            Assert.Equal("Invalid Reassignment", reassignment.title);
            auditServiceMock.Verify(auditService => auditService.ReassignShift(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        private FatigueShiftAuditViewModel.ReassignmentResult ApplyReassignmentForSuccessfulFlow()
        {
            var violation = MakeViolation(shiftId: 3);
            var suggestion = MakeSuggestion(shiftId: 3, suggestedStaffId: 50, suggestedName: "Bob");
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            auditServiceMock.Setup(auditService => auditService.ReassignShift(3, 50)).Returns(true);
            return CreateViewModel().ApplyReassignment(shiftId: 3);
        }

        [Fact]
        public void ApplyReassignment_WhenReassignmentSucceeds_ReturnsSuccessFlag()
        {
            Assert.True(ApplyReassignmentForSuccessfulFlow().isSuccess);
        }

        [Fact]
        public void ApplyReassignment_WhenReassignmentSucceeds_ReturnsAppliedTitle()
        {
            Assert.Equal("Reassignment Applied", ApplyReassignmentForSuccessfulFlow().title);
        }

        [Fact]
        public void ApplyReassignment_WhenReassignmentSucceeds_MessageContainsCandidateName()
        {
            Assert.Contains("Bob", ApplyReassignmentForSuccessfulFlow().message);
        }

        [Fact]
        public void ApplyReassignment_ReturnsFailure_WhenServiceReturnsFalse()
        {
            var violation = MakeViolation(shiftId: 3);
            var suggestion = MakeSuggestion(shiftId: 3, suggestedStaffId: 50);
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            auditServiceMock.Setup(auditService => auditService.ReassignShift(3, 50)).Returns(false);
            var viewModel = CreateViewModel();

            var reassignment = viewModel.ApplyReassignment(shiftId: 3);

            Assert.False(reassignment.isSuccess);
            Assert.Equal("Reassignment Failed", reassignment.title);
        }

        [Fact]
        public void ApplyReassignment_CallsRunAutoAudit_AfterSuccessfulReassignment()
        {
            var violation = MakeViolation(shiftId: 3);
            var suggestion = MakeSuggestion(shiftId: 3, suggestedStaffId: 50);
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            auditServiceMock.Setup(auditService => auditService.ReassignShift(3, 50)).Returns(true);
            var viewModel = CreateViewModel();

            viewModel.ApplyReassignment(shiftId: 3);

            auditServiceMock.Verify(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()), Times.Exactly(2));
        }

        [Fact]
        public void ApplyReassignment_DoesNotCallRunAutoAudit_WhenServiceFails()
        {
            var violation = MakeViolation(shiftId: 3);
            var suggestion = MakeSuggestion(shiftId: 3, suggestedStaffId: 50);
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolationsAndSuggestions(
                    new[] { violation },
                    new[] { suggestion }));
            auditServiceMock.Setup(auditService => auditService.ReassignShift(3, 50)).Returns(false);
            var viewModel = CreateViewModel();

            viewModel.ApplyReassignment(shiftId: 3);

            auditServiceMock.Verify(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public void PublishStatus_IsReady_WhenCanPublish()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var viewModel = CreateViewModel();

            Assert.Equal("Publish status: READY", viewModel.PublishStatus);
        }

        [Fact]
        public void PublishStatus_IsBlocked_WhenCannotPublish()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()));
            var viewModel = CreateViewModel();

            Assert.Equal("Publish status: BLOCKED", viewModel.PublishStatus);
        }

        [Fact]
        public void PublishStatusDescription_IsPositive_WhenCanPublish()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(CleanResult());
            var viewModel = CreateViewModel();

            Assert.Contains("No violations", viewModel.PublishStatusDescription);
        }

        [Fact]
        public void PublishStatusDescription_IsNegative_WhenCannotPublish()
        {
            auditServiceMock
                .Setup(auditService => auditService.RunAutoAudit(It.IsAny<DateTime>()))
                .Returns(ResultWithViolations(MakeViolation()));
            var viewModel = CreateViewModel();

            Assert.Contains("cannot be published", viewModel.PublishStatusDescription);
        }
    }
}
