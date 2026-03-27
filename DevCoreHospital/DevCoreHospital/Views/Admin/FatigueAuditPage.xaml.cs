using DevCoreHospital.Data;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DevCoreHospital.Views.Admin
{
    public sealed partial class FatigueAuditPage : Page, INotifyPropertyChanged
    {
        private readonly FatigueShiftAuditViewModel _viewModel;
        private readonly IFatigueAuditService _auditService;
        private readonly MockFatigueShiftDataSource _mockDataSource;

        public ObservableCollection<FatigueShiftAuditViewModel.AuditViolationRow> Violations { get; } = new();
        public ObservableCollection<FatigueShiftAuditViewModel.AutoSuggestRow> Suggestions { get; } = new();

        private string _statusMessage = "Select a week and run the auto-audit to check for fatigue violations.";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _weekLabel = "Week of Today";
        public string WeekLabel
        {
            get => _weekLabel;
            set
            {
                if (_weekLabel != value)
                {
                    _weekLabel = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _publishStatus = "Publish status: BLOCKED";
        public string PublishStatus
        {
            get => _publishStatus;
            set
            {
                if (_publishStatus != value)
                {
                    _publishStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _publishStatusDescription = "Roster cannot be published while violations exist. Run audit and resolve all conflicts.";
        public string PublishStatusDescription
        {
            get => _publishStatusDescription;
            set
            {
                if (_publishStatusDescription != value)
                {
                    _publishStatusDescription = value;
                    OnPropertyChanged();
                }
            }
        }

        private Brush _publishStatusColor = new SolidColorBrush(Colors.Red);
        public Brush PublishStatusColor
        {
            get => _publishStatusColor;
            set
            {
                if (_publishStatusColor != value)
                {
                    _publishStatusColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _canPublish = false;
        public bool CanPublish
        {
            get => _canPublish;
            set
            {
                if (_canPublish != value)
                {
                    _canPublish = value;
                    OnPropertyChanged();
                    UpdatePublishStatus();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public FatigueAuditPage()
        {
            InitializeComponent();

            // Initialize mock data source and service
            _mockDataSource = new MockFatigueShiftDataSource();
            _auditService = new FatigueAuditService(_mockDataSource);
            
            // Initialize ViewModel
            _viewModel = new FatigueShiftAuditViewModel(_auditService);

            // Set DataContext for binding
            DataContext = this;

            // Initialize week picker to today
            WeekStartPicker.Date = new DateTimeOffset(DateTime.Today);
            UpdateWeekLabel();
        }

        private void WeekStartPicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (sender.Date.HasValue)
            {
                _viewModel.SelectedWeekStart = sender.Date.Value;
                UpdateWeekLabel();
            }
        }

        private void UpdateWeekLabel()
        {
            var date = _viewModel.SelectedWeekStart.Date;
            WeekLabel = $"Week of {date:dddd, MMMM d, yyyy}";
        }

        private void RunAutoAudit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusMessage = "Running audit...";
                
                // Run the audit
                _viewModel.RunAutoAudit();

                // Sync violations and suggestions from ViewModel to UI
                Violations.Clear();
                foreach (var violation in _viewModel.Violations)
                {
                    Violations.Add(violation);
                }

                Suggestions.Clear();
                foreach (var suggestion in _viewModel.Suggestions)
                {
                    Suggestions.Add(suggestion);
                }

                // Update UI state
                CanPublish = _viewModel.CanPublish;

                // Update status message
                StatusMessage = _viewModel.StatusMessage;

                if (CanPublish)
                {
                    PublishStatusDescription = "✓ No violations detected. Roster is ready to publish.";
                }
                else
                {
                    PublishStatusDescription = $"✗ {Violations.Count} conflict(s) found. Review violations and suggestions to proceed.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during audit: {ex.Message}";
                CanPublish = false;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Violations.Clear();
            Suggestions.Clear();
            StatusMessage = "Select a week and run the auto-audit to check for fatigue violations.";
            CanPublish = false;
            PublishStatusDescription = "Roster cannot be published while violations exist. Run audit and resolve all conflicts.";
        }

        private async void ApplyReassignment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int shiftId)
            {
                // Find the suggestion for this shift
                var suggestion = _viewModel.Suggestions.FirstOrDefault(s => s.ShiftId == shiftId);
                if (suggestion == null || !suggestion.SuggestedStaffId.HasValue)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Invalid Reassignment",
                        Content = "No valid reassignment candidate found for this shift.",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }

                // Apply the reassignment to the mock data source
                bool success = _mockDataSource.ReassignShift(shiftId, suggestion.SuggestedStaffId.Value);
                if (success)
                {
                    // Show confirmation dialog
                    var confirmDialog = new ContentDialog
                    {
                        Title = "Reassignment Applied",
                        Content = $"Shift #{shiftId} has been reassigned to {suggestion.SuggestedStaffName}.\n\nRunning audit to verify changes...",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };
                    await confirmDialog.ShowAsync();

                    // Auto-run audit again to show updated violations
                    RunAutoAudit_Click(null, null);
                }
                else
                {
                    var failDialog = new ContentDialog
                    {
                        Title = "Reassignment Failed",
                        Content = "Could not reassign shift. Please try again.",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };
                    await failDialog.ShowAsync();
                }
            }
        }

        private void PublishRoster_Click(object sender, RoutedEventArgs e)
        {
            if (CanPublish)
            {
                var dialog = new ContentDialog
                {
                    Title = "Roster Published",
                    Content = $"The roster for the week of {WeekLabel} has been published successfully.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                dialog.ShowAsync();
            }
        }

        private void UpdatePublishStatus()
        {
            if (CanPublish)
            {
                PublishStatus = "Publish status: READY";
                PublishStatusColor = new SolidColorBrush(Colors.Green);
            }
            else
            {
                PublishStatus = "Publish status: BLOCKED";
                PublishStatusColor = new SolidColorBrush(Colors.Red);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

