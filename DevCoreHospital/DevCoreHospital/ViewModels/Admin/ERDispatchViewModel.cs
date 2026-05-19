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
        private const string UnknownDoctorName = "Unknown";
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

            async Task SimulateDefaultCount() => await SimulateIncomingAsync(DefaultSimulatedRequestCount);
            SimulateIncomingCommand = new AsyncRelayCommand(SimulateDefaultCount);

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

        public async Task SimulateIncomingAsync(int requestCount)
        {
            try
            {
                var createdRequestIds = await dispatchService.SimulateIncomingRequestsAsync(requestCount);
                StatusMessage = $"Simulated {createdRequestIds.Count} incoming request(s) from Clinical Team. Click Run Dispatch.";
                ManualInterventionHint = "Incoming ER requests were added as PENDING.";
            }
            catch (Exception exception)
            {
                StatusMessage = $"Error: {exception.Message}";
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
                    await LoadOverrideCandidatesAsync(UnmatchedRequests.First().RequestId);
                }
                else
                {
                    ManualInterventionHint = "No unmatched requests. Override not needed.";
                }
            }
            catch (Exception exception)
            {
                StatusMessage = $"Error: {exception.Message}";
            }
        }

        public async Task LoadOverrideCandidatesAsync(int requestId)
        {
            var overrideCandidateDoctors = await dispatchService.GetManualOverrideCandidatesAsync(requestId, NearEndMinutesThreshold);
            OverrideCandidates.ReplaceWith(overrideCandidateDoctors.Select(OverrideCandidateRow.From));

            ManualInterventionHint = OverrideCandidates.Count == 0
                ? "No eligible override doctor found (need near-end IN_EXAMINATION doctor)."
                : $"Found {OverrideCandidates.Count} eligible override candidate(s).";
        }

        public async Task<bool> ApplyOverrideAsync(int requestId, int doctorId)
        {
            bool MatchesRequestId(UnmatchedRequestRow unmatchedRow) => unmatchedRow.RequestId == requestId;
            var unmatchedRequest = UnmatchedRequests.FirstOrDefault(MatchesRequestId);
            if (unmatchedRequest == null)
            {
                ManualInterventionHint = "Select an unmatched request first.";
                return false;
            }

            bool MatchesDoctorId(OverrideCandidateRow candidateRow) => candidateRow.DoctorId == doctorId;
            var overrideCandidate = OverrideCandidates.FirstOrDefault(MatchesDoctorId);
            if (overrideCandidate == null)
            {
                ManualInterventionHint = "Select an eligible override doctor first.";
                return false;
            }

            var overrideResult = await dispatchService.ManualOverrideAsync(requestId, doctorId, NearEndMinutesThreshold);
            if (!overrideResult.IsSuccess)
            {
                ManualInterventionHint = overrideResult.Message;
                return false;
            }

            ManualInterventionHint = overrideResult.Message;

            UnmatchedRequests.Remove(unmatchedRequest);
            SuccessfulMatches.Add(new SuccessfulMatchRow
            {
                RequestId = overrideResult.Request.Id,
                AssignedDoctor = overrideResult.MatchedDoctorName ?? UnknownDoctorName,
                Specialization = overrideResult.Request.Specialization,
                MatchReason = overrideResult.MatchReason,
            });

            StatusMessage = $"{SuccessfulMatches.Count} matched, {UnmatchedRequests.Count} unmatched";

            if (UnmatchedRequests.Count > 0)
            {
                await LoadOverrideCandidatesAsync(UnmatchedRequests.First().RequestId);
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
            var dispatchResult = await dispatchService.DispatchERRequestAsync(requestId);

            if (dispatchResult.IsSuccess)
            {
                SuccessfulMatches.Add(new SuccessfulMatchRow
                {
                    RequestId = dispatchResult.Request.Id,
                    AssignedDoctor = dispatchResult.MatchedDoctorName ?? UnknownDoctorName,
                    Specialization = dispatchResult.Request.Specialization,
                    MatchReason = dispatchResult.MatchReason,
                });
            }
            else
            {
                UnmatchedRequests.Add(new UnmatchedRequestRow
                {
                    RequestId = dispatchResult.Request.Id,
                    RequestSpecialization = dispatchResult.Request.Specialization,
                    RequestLocation = dispatchResult.Request.Location,
                    NoMatchReason = dispatchResult.Message,
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

            private bool HasKnownTimeRemaining => MinutesToEnd >= 0;

            public string DisplayLabel => HasKnownTimeRemaining
                ? $"{FullName} (ends in {MinutesToEnd} min)"
                : FullName;

            public static OverrideCandidateRow From(DoctorProfile candidate) =>
                new OverrideCandidateRow
                {
                    DoctorId = candidate.DoctorId,
                    FullName = candidate.FullName,
                    MinutesToEnd = candidate.MinutesToEnd,
                };
        }
    }
}