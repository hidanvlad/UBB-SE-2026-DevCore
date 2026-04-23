using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace DevCoreHospital.ViewModels
{
    public sealed class FatigueShiftAuditViewModel : ObservableObject
    {
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

        public string WeekLabel
        {
            get
            {
                var englishCulture = CultureInfo.GetCultureInfo("en-US");
                return $"Week of {SelectedWeekStart.ToString("dd MMM yyyy", englishCulture)}";
            }
        }

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
                    RaisePropertyChanged(nameof(PublishStatusColor));
                    RaisePropertyChanged(nameof(PublishStatusDescription));
                }
            }
        }

        public string PublishStatus => CanPublish ? "Publish status: READY" : "Publish status: BLOCKED";

        public Brush PublishStatusColor => CanPublish
            ? new SolidColorBrush(Colors.Green)
            : new SolidColorBrush(Colors.Red);

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
            var result = auditService.RunAutoAudit(SelectedWeekStart.Date);
            var englishCulture = CultureInfo.GetCultureInfo("en-US");

            Violations.Clear();
            foreach (var violation in result.Violations.OrderBy(violation => violation.ShiftStart))
            {
                Violations.Add(new AuditViolationRow
                {
                    ShiftId = violation.ShiftId,
                    Staff = violation.StaffName,
                    Window = $"{violation.ShiftStart.ToString("ddd HH:mm", englishCulture)} - {violation.ShiftEnd.ToString("ddd HH:mm", englishCulture)}",
                    Rule = violation.Rule,
                    Message = violation.Message
                });
            }

            Suggestions.Clear();
            foreach (var suggestion in result.Suggestions.OrderBy(suggestion => suggestion.ShiftId))
            {
                Suggestions.Add(new AutoSuggestRow
                {
                    ShiftId = suggestion.ShiftId,
                    ReassignText = suggestion.SuggestedStaffId.HasValue
                        ? $"Shift #{suggestion.ShiftId}: {suggestion.OriginalStaffName} -> {suggestion.SuggestedStaffName}"
                        : $"Shift #{suggestion.ShiftId}: no replacement candidate",
                    Reason = suggestion.Reason,
                    SuggestedStaffId = suggestion.SuggestedStaffId,
                    SuggestedStaffName = suggestion.SuggestedStaffName
                });
            }

            CanPublish = result.CanPublish;
            StatusMessage = result.Summary;
            RaisePropertyChanged(nameof(HasConflicts));
        }

        public bool HasConflicts => Violations.Count > 0;

        public ReassignmentResult ApplyReassignment(int shiftId)
        {
            var suggestion = Suggestions.FirstOrDefault(auditSuggestion => auditSuggestion.ShiftId == shiftId);
            if (suggestion == null || !suggestion.SuggestedStaffId.HasValue)
            {
                return new ReassignmentResult(
                    false,
                    "Invalid Reassignment",
                    "No valid reassignment candidate found for this shift.");
            }

            bool isSuccess = auditService.ReassignShift(shiftId, suggestion.SuggestedStaffId.Value);
            if (!isSuccess)
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
                $"Shift #{shiftId} has been reassigned to {suggestion.SuggestedStaffName}.\n\nAudit was re-run to verify changes.");
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
            public string ReassignText { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public int? SuggestedStaffId { get; set; }
            public string SuggestedStaffName { get; set; } = string.Empty;
        }
    }
}
