using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels
{
    public sealed class ERDispatchViewModel : ObservableObject
    {
        private const int NearEndMinutesThreshold = 30;
        private const int DefaultSimulatedRequestCount = 3;
        private readonly IERDispatchService dispatchService;

        public ObservableCollection<UnmatchedRequestRow> UnmatchedRequests { get; } = new ObservableCollection<UnmatchedRequestRow>();
        public ObservableCollection<SuccessfulMatchRow> SuccessfulMatches { get; } = new ObservableCollection<SuccessfulMatchRow>();
        public ObservableCollection<OverrideCandidateRow> OverrideCandidates { get; } = new ObservableCollection<OverrideCandidateRow>();

        private string statusMessage = "Ready";
        public string StatusMessage
        {
            get => statusMessage;
            private set => SetProperty(ref statusMessage, value);
        }

        private string manualInterventionHint = "Manual override accepts near-end IN_EXAMINATION doctors only.";
        public string ManualInterventionHint
        {
            get => manualInterventionHint;
            private set => SetProperty(ref manualInterventionHint, value);
        }

        public AsyncRelayCommand RunDispatchCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public AsyncRelayCommand SimulateIncomingCommand { get; }

        public ERDispatchViewModel(IERDispatchService dispatchService)
        {
            this.dispatchService = dispatchService;
            RunDispatchCommand = new AsyncRelayCommand(RunDispatchAsync);
            RefreshCommand = new RelayCommand(Refresh);
            SimulateIncomingCommand = new AsyncRelayCommand(() => SimulateIncomingAsync(DefaultSimulatedRequestCount));

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
                var createdIds = await dispatchService.SimulateIncomingRequestsAsync(count);
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
                var pendingRequestIds = await dispatchService.GetPendingRequestIdsAsync();

                foreach (var requestId in pendingRequestIds)
                {
                    await HandleRequestByIdAsync(requestId);
                }

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
            var candidates = await dispatchService.GetManualOverrideCandidatesAsync(requestId, NearEndMinutesThreshold);

            foreach (var candidate in candidates)
            {
                var minutesToEnd = candidate.ScheduleEnd.HasValue
                    ? Math.Max(0, (int)Math.Round((candidate.ScheduleEnd.Value - DateTime.Now).TotalMinutes))
                    : -1;

                OverrideCandidates.Add(new OverrideCandidateRow
                {
                    DoctorId = candidate.DoctorId,
                    FullName = candidate.FullName,
                    MinutesToEnd = minutesToEnd
                });
            }

            ManualInterventionHint = OverrideCandidates.Count == 0
                ? "No eligible override doctor found (need near-end IN_EXAMINATION doctor)."
                : $"Found {OverrideCandidates.Count} eligible override candidate(s).";
        }

        public async Task<bool> ApplyOverrideAsync(int requestId, int doctorId)
        {
            var unmatchedRequest = UnmatchedRequests.FirstOrDefault(unmatchedRequest => unmatchedRequest.RequestId == requestId);
            if (unmatchedRequest == null)
            {
                ManualInterventionHint = "Select an unmatched request first.";
                return false;
            }

            var overrideCandidate = OverrideCandidates.FirstOrDefault(overrideCandidate => overrideCandidate.DoctorId == doctorId);
            if (overrideCandidate == null)
            {
                ManualInterventionHint = "Select an eligible override doctor first.";
                return false;
            }

            var result = await dispatchService.ManualOverrideAsync(requestId, doctorId, NearEndMinutesThreshold);
            if (!result.IsSuccess)
            {
                ManualInterventionHint = result.Message;
                return false;
            }

            ManualInterventionHint = result.Message;

            UnmatchedRequests.Remove(unmatchedRequest);
            SuccessfulMatches.Add(new SuccessfulMatchRow
            {
                RequestId = result.Request.Id,
                AssignedDoctor = result.MatchedDoctorName ?? "Unknown",
                Specialization = result.Request.Specialization,
                MatchReason = result.MatchReason,
            });

            StatusMessage = $"{SuccessfulMatches.Count} matched, {UnmatchedRequests.Count} unmatched";

            if (UnmatchedRequests.Count > 0)
            {
                await LoadOverrideCandidatesAsync(UnmatchedRequests[0].RequestId);
            }
            else
            {
                OverrideCandidates.Clear();
                ManualInterventionHint = "Override applied. No unmatched requests left.";
            }

            return true;
        }

        private async Task HandleRequestByIdAsync(int requestId)
        {
            var result = await dispatchService.DispatchERRequestAsync(requestId);

            if (result.IsSuccess)
            {
                SuccessfulMatches.Add(new SuccessfulMatchRow
                {
                    RequestId = result.Request.Id,
                    AssignedDoctor = result.MatchedDoctorName ?? "Unknown",
                    Specialization = result.Request.Specialization,
                    MatchReason = result.MatchReason,
                });
            }
            else
            {
                UnmatchedRequests.Add(new UnmatchedRequestRow
                {
                    RequestId = result.Request.Id,
                    RequestSpecialization = result.Request.Specialization,
                    RequestLocation = result.Request.Location,
                    NoMatchReason = result.Message,
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
