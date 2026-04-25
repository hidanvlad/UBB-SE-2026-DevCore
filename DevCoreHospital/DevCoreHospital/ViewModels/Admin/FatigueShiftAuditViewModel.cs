using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels
{
    public sealed class FatigueShiftAuditViewModel : ObservableObject
    {
        private const string EnglishCultureCode = "en-US";
        private const string WeeklyDateFormat = "dd MMM yyyy";
        private const string ShiftTimeFormat = "ddd HH:mm";

        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo(EnglishCultureCode);

        private readonly IFatigueAuditService auditService;

        public ObservableCollection<AuditViolationRow> Violations { get; } = new ObservableCollection<AuditViolationRow>();
        public ObservableCollection<AutoSuggestRow> Suggestions { get; } = new ObservableCollection<AutoSuggestRow>();

        private DateTimeOffset selectedWeekStart = new DateTimeOffset(StartOfWeek(DateTime.Today));
        public DateTimeOffset SelectedWeekStart
        {
            get => selectedWeekStart;
            set
            {
                var normalized = new DateTimeOffset(StartOfWeek(value.Date));
                if (SetProperty(ref selectedWeekStart, normalized))
                {
                    RaisePropertyChanged(nameof(WeekLabel));
                    RunAutoAuditCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string WeekLabel =>
            $"Week of {SelectedWeekStart.ToString(WeeklyDateFormat, EnglishCulture)}";

        private string statusMessage = "Run Auto-Audit to validate this roster.";
        public string StatusMessage
        {
            get => statusMessage;
            set => SetProperty(ref statusMessage, value);
        }

        private bool canPublish;
        public bool CanPublish
        {
            get => canPublish;
            private set
            {
                if (SetProperty(ref canPublish, value))
                {
                    RaisePropertyChanged(nameof(PublishStatus));
                    RaisePropertyChanged(nameof(PublishStatusDescription));
                }
            }
        }

        public string PublishStatus => CanPublish ? "Publish status: READY" : "Publish status: BLOCKED";

        public string PublishStatusDescription => CanPublish
            ? "? No violations detected. Roster is ready to publish."
            : "Roster cannot be published while violations exist. Run audit and resolve all conflicts.";

        public RelayCommand RunAutoAuditCommand { get; }

        public FatigueShiftAuditViewModel(IFatigueAuditService auditService)
        {
            this.auditService = auditService;
            RunAutoAuditCommand = new RelayCommand(RunAutoAudit);

            RunAutoAudit();
        }

        public void RunAutoAudit()
        {
            var auditResult = auditService.RunAutoAudit(SelectedWeekStart.Date);

            Violations.Clear();
            DateTime GetViolationShiftStart(Models.AuditViolation violation) => violation.ShiftStart;
            foreach (var violation in auditResult.Violations.OrderBy(GetViolationShiftStart))
            {
                Violations.Add(new AuditViolationRow
                {
                    ShiftId = violation.ShiftId,
                    Staff = violation.StaffName,
                    Window = $"{violation.ShiftStart.ToString(ShiftTimeFormat, EnglishCulture)} - {violation.ShiftEnd.ToString(ShiftTimeFormat, EnglishCulture)}",
                    Rule = violation.Rule,
                    Message = violation.Message
                });
            }

            Suggestions.Clear();
            int GetSuggestionShiftId(Models.AutoSuggestRecommendation suggestion) => suggestion.ShiftId;
            foreach (var suggestion in auditResult.Suggestions.OrderBy(GetSuggestionShiftId))
            {
                Suggestions.Add(new AutoSuggestRow
                {
                    ShiftId = suggestion.ShiftId,
                    ReassignmentLabel = suggestion.SuggestedStaffId.HasValue
                        ? $"Shift #{suggestion.ShiftId}: {suggestion.OriginalStaffName} -> {suggestion.SuggestedStaffName}"
                        : $"Shift #{suggestion.ShiftId}: no replacement candidate",
                    Reason = suggestion.Reason,
                    SuggestedStaffId = suggestion.SuggestedStaffId,
                    SuggestedStaffName = suggestion.SuggestedStaffName
                });
            }

            CanPublish = auditResult.CanPublish;
            StatusMessage = auditResult.Summary;
            RaisePropertyChanged(nameof(HasConflicts));
        }

        public bool HasConflicts => Violations.Any();

        public ReassignmentResult ApplyReassignment(int shiftId)
        {
            bool IsMatchingShift(AutoSuggestRow auditSuggestion) => auditSuggestion.ShiftId == shiftId;
            var matchedSuggestion = Suggestions.FirstOrDefault(IsMatchingShift);
            if (matchedSuggestion == null || !matchedSuggestion.SuggestedStaffId.HasValue)
            {
                return new ReassignmentResult(
                    false,
                    "Invalid Reassignment",
                    "No valid reassignment candidate found for this shift.");
            }

            bool reassignmentSucceeded = auditService.ReassignShift(shiftId, matchedSuggestion.SuggestedStaffId.Value);
            if (!reassignmentSucceeded)
            {
                return new ReassignmentResult(
                    false,
                    "Reassignment Failed",
                    "Could not reassign shift. Please try again.");
            }

            RunAutoAudit();

            return new ReassignmentResult(
                true,
                "Reassignment Applied",
                $"Shift #{shiftId} has been reassigned to {matchedSuggestion.SuggestedStaffName}.\n\nAudit was re-run to verify changes.");
        }

        public sealed record ReassignmentResult(bool isSuccess, string title, string message);

        private static DateTime StartOfWeek(DateTime date)
        {
            const int daysInWeek = 7;
            var daysFromMonday = (daysInWeek + (date.DayOfWeek - DayOfWeek.Monday)) % daysInWeek;
            return date.Date.AddDays(-daysFromMonday);
        }

        public sealed class AuditViolationRow
        {
            public int ShiftId { get; set; }
            public string Staff { get; set; } = string.Empty;
            public string Window { get; set; } = string.Empty;
            public string Rule { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        public sealed class AutoSuggestRow
        {
            public int ShiftId { get; set; }
            public string ReassignmentLabel { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public int? SuggestedStaffId { get; set; }
            public string SuggestedStaffName { get; set; } = string.Empty;
        }
    }
}