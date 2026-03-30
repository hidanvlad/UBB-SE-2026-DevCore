using DevCoreHospital.Data;
using DevCoreHospital.Configuration;
using DevCoreHospital.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DevCoreHospital.Views
{
    public sealed partial class ERDispatchPage : Page, INotifyPropertyChanged
    {
        private readonly IERDispatchService _dispatchService;
        private const int NearEndMinutesThreshold = 30;

        public ObservableCollection<UnmatchedRequestRow> UnmatchedRequests { get; }
        public ObservableCollection<SuccessfulMatchRow> SuccessfulMatches { get; }
        public ObservableCollection<OverrideCandidateRow> OverrideCandidates { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value)
                    return;

                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        private string _manualInterventionHint = "Manual override accepts near-end IN_EXAMINATION or not-working doctors.";
        public string ManualInterventionHint
        {
            get => _manualInterventionHint;
            set
            {
                if (_manualInterventionHint == value)
                    return;

                _manualInterventionHint = value;
                OnPropertyChanged();
            }
        }

        public ERDispatchPage()
        {
            InitializeComponent();

            _dispatchService = new ERDispatchService(new SqlERDispatchDataSource(AppSettings.ConnectionString));

            UnmatchedRequests = new ObservableCollection<UnmatchedRequestRow>();
            SuccessfulMatches = new ObservableCollection<SuccessfulMatchRow>();
            OverrideCandidates = new ObservableCollection<OverrideCandidateRow>();

            DataContext = this;
        }

        private async void RunDispatch_Click(object sender, RoutedEventArgs e)
        {
            UnmatchedRequests.Clear();
            SuccessfulMatches.Clear();
            OverrideCandidates.Clear();
            StatusMessage = "Dispatching...";
            ManualInterventionHint = "Manual override accepts near-end IN_EXAMINATION or not-working doctors.";

            try
            {
                var pendingList = await _dispatchService.GetPendingRequestIdsAsync();

                foreach (var requestId in pendingList)
                {
                    var result = await _dispatchService.DispatchERRequestAsync(requestId);

                    if (result.IsSuccess)
                    {
                        SuccessfulMatches.Add(new SuccessfulMatchRow
                        {
                            RequestId = result.Request.Id,
                            AssignedDoctor = result.MatchedDoctorName ?? "Unknown",
                            Specialization = result.Request.Specialization,
                            MatchReason = result.MatchReason
                        });
                    }
                    else
                    {
                        UnmatchedRequests.Add(new UnmatchedRequestRow
                        {
                            RequestId = result.Request.Id,
                            RequestSpecialization = result.Request.Specialization,
                            RequestLocation = result.Request.Location,
                            NoMatchReason = result.Message
                        });
                    }
                }

                StatusMessage = $"{SuccessfulMatches.Count} matched, {UnmatchedRequests.Count} unmatched";

                if (UnmatchedRequests.Count > 0)
                {
                    UnmatchedRequestCombo.SelectedIndex = 0;
                    await LoadOverrideCandidatesAsync(UnmatchedRequests[0].RequestId);
                }
                else
                {
                    ManualInterventionHint = "No unmatched requests. Override not needed.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            UnmatchedRequests.Clear();
            SuccessfulMatches.Clear();
            OverrideCandidates.Clear();
            StatusMessage = "Ready";
            ManualInterventionHint = "Manual override accepts near-end IN_EXAMINATION or not-working doctors.";
        }

        private async void SimulateIncoming_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var createdIds = await _dispatchService.SimulateIncomingRequestsAsync(3);
                StatusMessage = $"Simulated {createdIds.Count} incoming request(s) from Clinical Team. Click Run Dispatch.";
                ManualInterventionHint = "Incoming ER requests were added as PENDING.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private async void UnmatchedRequestCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UnmatchedRequestCombo.SelectedItem is UnmatchedRequestRow row)
                await LoadOverrideCandidatesAsync(row.RequestId);
        }

        private async void ApplyOverride_Click(object sender, RoutedEventArgs e)
        {
            if (UnmatchedRequestCombo.SelectedItem is not UnmatchedRequestRow req)
            {
                ManualInterventionHint = "Select an unmatched request first.";
                return;
            }

            if (OverrideDoctorCombo.SelectedItem is not OverrideCandidateRow candidate)
            {
                ManualInterventionHint = "Select an eligible override doctor first.";
                return;
            }

            var result = await _dispatchService.ManualOverrideAsync(req.RequestId, candidate.DoctorId, NearEndMinutesThreshold);
            if (!result.IsSuccess)
            {
                ManualInterventionHint = result.Message;
                return;
            }

            ManualInterventionHint = result.Message;

            UnmatchedRequests.Remove(req);
            SuccessfulMatches.Add(new SuccessfulMatchRow
            {
                RequestId = result.Request.Id,
                AssignedDoctor = result.MatchedDoctorName ?? "Unknown",
                Specialization = result.Request.Specialization,
                MatchReason = result.MatchReason
            });

            StatusMessage = $"{SuccessfulMatches.Count} matched, {UnmatchedRequests.Count} unmatched";

            OverrideCandidates.Clear();
            if (UnmatchedRequests.Count > 0)
            {
                UnmatchedRequestCombo.SelectedIndex = 0;
                await LoadOverrideCandidatesAsync(UnmatchedRequests[0].RequestId);
            }
            else
            {
                ManualInterventionHint = "Override applied. No unmatched requests left.";
            }
        }

        private async System.Threading.Tasks.Task LoadOverrideCandidatesAsync(int requestId)
        {
            OverrideCandidates.Clear();
            var candidates = await _dispatchService.GetManualOverrideCandidatesAsync(requestId, NearEndMinutesThreshold);

            foreach (var c in candidates)
            {
                var minutesToEnd = c.ScheduleEnd.HasValue
                    ? Math.Max(0, (int)Math.Round((c.ScheduleEnd.Value - DateTime.Now).TotalMinutes))
                    : -1;

                OverrideCandidates.Add(new OverrideCandidateRow
                {
                    DoctorId = c.DoctorId,
                    FullName = c.FullName,
                    MinutesToEnd = minutesToEnd
                });
            }

            ManualInterventionHint = OverrideCandidates.Count == 0
                ? $"No eligible override doctor found (need near-end IN_EXAMINATION or not-working doctor)."
                : $"Found {OverrideCandidates.Count} eligible override candidate(s).";
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public sealed class UnmatchedRequestRow
        {
            public int RequestId { get; set; }
            public string RequestSpecialization { get; set; } = string.Empty;
            public string RequestLocation { get; set; } = string.Empty;
            public string NoMatchReason { get; set; } = string.Empty;

            public string RequestLabel => $"#{RequestId} - {RequestSpecialization} @ {RequestLocation}";
        }

        public sealed class SuccessfulMatchRow
        {
            public int RequestId { get; set; }
            public string AssignedDoctor { get; set; } = string.Empty;
            public string Specialization { get; set; } = string.Empty;
            public string MatchReason { get; set; } = string.Empty;
        }

        public sealed class OverrideCandidateRow
        {
            public int DoctorId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public int MinutesToEnd { get; set; }

            public string DisplayLabel => MinutesToEnd >= 0
                ? $"{FullName} (ends in {MinutesToEnd} min)"
                : FullName;
        }
    }
}

