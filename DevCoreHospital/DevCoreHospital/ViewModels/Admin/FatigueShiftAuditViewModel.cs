using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace DevCoreHospital.ViewModels
{
    public sealed class FatigueShiftAuditViewModel : ObservableObject
    {
        private readonly IFatigueAuditService _auditService;

        public ObservableCollection<AuditViolationRow> Violations { get; } = new();
        public ObservableCollection<AutoSuggestRow> Suggestions { get; } = new();

        private DateTimeOffset _selectedWeekStart = new DateTimeOffset(StartOfWeek(DateTime.Today));
        public DateTimeOffset SelectedWeekStart
        {
            get => _selectedWeekStart;
            set
            {
                var normalized = new DateTimeOffset(StartOfWeek(value.Date));
                if (SetProperty(ref _selectedWeekStart, normalized))
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

        private string _statusMessage = "Run Auto-Audit to validate this roster.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _canPublish = false;
        public bool CanPublish
        {
            get => _canPublish;
            private set
            {
                if (SetProperty(ref _canPublish, value))
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
            ? "✓ No violations detected. Roster is ready to publish."
            : "Roster cannot be published while violations exist. Run audit and resolve all conflicts.";

        public RelayCommand RunAutoAuditCommand { get; }

        public FatigueShiftAuditViewModel(IFatigueAuditService auditService)
        {
            _auditService = auditService;
            RunAutoAuditCommand = new RelayCommand(RunAutoAudit);

            RunAutoAudit();
        }

        public void RunAutoAudit()
        {
            var result = _auditService.RunAutoAudit(SelectedWeekStart.Date);
            var englishCulture = CultureInfo.GetCultureInfo("en-US");

            Violations.Clear();
            foreach (var v in result.Violations.OrderBy(x => x.ShiftStart))
            {
                Violations.Add(new AuditViolationRow
                {
                    ShiftId = v.ShiftId,
                    Staff = v.StaffName,
                    Window = $"{v.ShiftStart.ToString("ddd HH:mm", englishCulture)} - {v.ShiftEnd.ToString("ddd HH:mm", englishCulture)}",
                    Rule = v.Rule,
                    Message = v.Message
                });
            }

            Suggestions.Clear();
            foreach (var s in result.Suggestions.OrderBy(x => x.ShiftId))
            {
                Suggestions.Add(new AutoSuggestRow
                {
                    ShiftId = s.ShiftId,
                    ReassignText = s.SuggestedStaffId.HasValue
                        ? $"Shift #{s.ShiftId}: {s.OriginalStaffName} -> {s.SuggestedStaffName}"
                        : $"Shift #{s.ShiftId}: no replacement candidate",
                    Reason = s.Reason,
                    SuggestedStaffId = s.SuggestedStaffId,
                    SuggestedStaffName = s.SuggestedStaffName
                });
            }

            CanPublish = result.CanPublish;
            StatusMessage = result.Summary;
            RaisePropertyChanged(nameof(HasConflicts));
        }

        public bool HasConflicts => Violations.Count > 0;

        /// <summary>
        /// Applies the audit-recommended reassignment for a shift and re-runs the audit.
        /// Returns a human-readable result status used by the view.
        /// </summary>
        public ReassignmentResult ApplyReassignment(int shiftId)
        {
            var suggestion = Suggestions.FirstOrDefault(s => s.ShiftId == shiftId);
            if (suggestion == null || !suggestion.SuggestedStaffId.HasValue)
            {
                return new ReassignmentResult(
                    false,
                    "Invalid Reassignment",
                    "No valid reassignment candidate found for this shift.");
            }

            bool success = _auditService.ReassignShift(shiftId, suggestion.SuggestedStaffId.Value);
            if (!success)
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

        public sealed record ReassignmentResult(bool Success, string Title, string Message);

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
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

