using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
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

        public string WeekLabel => $"Week of {SelectedWeekStart:dd MMM yyyy}";

        private string _statusMessage = "Run Auto-Audit to validate this roster.";
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        private bool _canPublish = false;
        public bool CanPublish
        {
            get => _canPublish;
            private set
            {
                if (SetProperty(ref _canPublish, value))
                    RaisePropertyChanged(nameof(PublishStatus));
            }
        }

        public string PublishStatus => CanPublish ? "Publish status: READY" : "Publish status: BLOCKED";

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

            Violations.Clear();
            foreach (var v in result.Violations.OrderBy(x => x.ShiftStart))
            {
                Violations.Add(new AuditViolationRow
                {
                    ShiftId = v.ShiftId,
                    Staff = v.StaffName,
                    Window = $"{v.ShiftStart:ddd HH:mm} - {v.ShiftEnd:ddd HH:mm}",
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

