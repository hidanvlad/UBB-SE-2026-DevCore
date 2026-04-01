using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DevCoreHospital.ViewModels
{
    public sealed class ERDispatchViewModel : ObservableObject
    {
        private const int NearEndMinutesThreshold = 30;
        private readonly IERDispatchService _dispatchService;

        public ObservableCollection<UnmatchedRequestRow> UnmatchedRequests { get; } = new();
        public ObservableCollection<SuccessfulMatchRow> SuccessfulMatches { get; } = new();
        public ObservableCollection<OverrideCandidateRow> OverrideCandidates { get; } = new();

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        private string _manualInterventionHint = "Manual override accepts near-end IN_EXAMINATION doctors only.";
        public string ManualInterventionHint
        {
            get => _manualInterventionHint;
            private set => SetProperty(ref _manualInterventionHint, value);
        }

        public AsyncRelayCommand RunDispatchCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public AsyncRelayCommand SimulateIncomingCommand { get; }

        public ERDispatchViewModel(IERDispatchService dispatchService)
        {
            _dispatchService = dispatchService;
            RunDispatchCommand = new AsyncRelayCommand(RunDispatchAsync);
            RefreshCommand = new RelayCommand(Refresh);
            SimulateIncomingCommand = new AsyncRelayCommand(() => SimulateIncomingAsync(3));

            Refresh();
        }

        
        public void LoadFlaggedRequests() => Refresh();

     
        public Task HandleERRequestAsync(ERRequest request)
            => request == null ? Task.CompletedTask : HandleRequestByIdAsync(request.Id);

      
        public Task OverrideAssignmentAsync(int doctorId, int requestId)
            => ApplyOverrideAsync(requestId, doctorId);

        public void Refresh()
        {
            UnmatchedRequests.Clear();
            SuccessfulMatches.Clear();
            OverrideCandidates.Clear();
            StatusMessage = "Ready";
            ManualInterventionHint = "Manual override accepts near-end IN_EXAMINATION doctors only.";
        }

        public async Task SimulateIncomingAsync(int count)
        {
            try
            {
                var createdIds = await _dispatchService.SimulateIncomingRequestsAsync(count);
                StatusMessage = $"Simulated {createdIds.Count} incoming request(s) from Clinical Team. Click Run Dispatch.";
                ManualInterventionHint = "Incoming ER requests were added as PENDING.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        public async Task RunDispatchAsync()
        {
            UnmatchedRequests.Clear();
            SuccessfulMatches.Clear();
            OverrideCandidates.Clear();
            StatusMessage = "Dispatching...";
            ManualInterventionHint = "Manual override accepts near-end IN_EXAMINATION doctors only.";

            try
            {
                var pendingList = await _dispatchService.GetPendingRequestIdsAsync();

                foreach (var requestId in pendingList)
                    await HandleRequestByIdAsync(requestId);

                StatusMessage = $"{SuccessfulMatches.Count} matched, {UnmatchedRequests.Count} unmatched";

                if (UnmatchedRequests.Count > 0)
                {
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

        public async Task LoadOverrideCandidatesAsync(int requestId)
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
                ? "No eligible override doctor found (need near-end IN_EXAMINATION doctor)."
                : $"Found {OverrideCandidates.Count} eligible override candidate(s).";
        }

        public async Task<bool> ApplyOverrideAsync(int requestId, int doctorId)
        {
            var req = UnmatchedRequests.FirstOrDefault(r => r.RequestId == requestId);
            if (req == null)
            {
                ManualInterventionHint = "Select an unmatched request first.";
                return false;
            }

            var candidate = OverrideCandidates.FirstOrDefault(c => c.DoctorId == doctorId);
            if (candidate == null)
            {
                ManualInterventionHint = "Select an eligible override doctor first.";
                return false;
            }

            var result = await _dispatchService.ManualOverrideAsync(requestId, doctorId, NearEndMinutesThreshold);
            if (!result.IsSuccess)
            {
                ManualInterventionHint = result.Message;
                return false;
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

            if (UnmatchedRequests.Count > 0)
                await LoadOverrideCandidatesAsync(UnmatchedRequests[0].RequestId);
            else
            {
                OverrideCandidates.Clear();
                ManualInterventionHint = "Override applied. No unmatched requests left.";
            }

            return true;
        }

        private async Task HandleRequestByIdAsync(int requestId)
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

