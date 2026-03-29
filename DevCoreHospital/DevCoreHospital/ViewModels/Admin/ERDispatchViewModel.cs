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
        private readonly IERDispatchService _dispatchService;

        public ObservableCollection<ERRequestRow> PendingRequests { get; } = new();
        public ObservableCollection<DispatchResultRow> DispatchResults { get; } = new();

        private string _statusMessage = "ER requests will appear here.";
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        private int _unmatchedCount = 0;
        public int UnmatchedCount
        {
            get => _unmatchedCount;
            private set => SetProperty(ref _unmatchedCount, value);
        }

        public AsyncRelayCommand RunDispatchCommand { get; }
        public OldRelayCommand RefreshCommand { get; }

        public ERDispatchViewModel(IERDispatchService dispatchService)
        {
            _dispatchService = dispatchService;
            RunDispatchCommand = new AsyncRelayCommand(RunDispatch);
            RefreshCommand = new OldRelayCommand(RefreshPending);

            RefreshPending();
        }

        private void RefreshPending()
        {
            PendingRequests.Clear();
            DispatchResults.Clear();
            UnmatchedCount = 0;
            StatusMessage = "Ready. Click 'Run Dispatch' to process pending requests.";
        }

        private async Task RunDispatch()
        {
            PendingRequests.Clear();
            DispatchResults.Clear();
            StatusMessage = "Running ER dispatch...";

            try
            {
                // Mock: simulate loading pending requests
                var pending = new[]
                {
                    new ERRequestRow { Id = 101, Specialization = "Cardiology", Location = "ER_MAIN", CreatedAt = DateTime.Now.AddMinutes(-15) },
                    new ERRequestRow { Id = 102, Specialization = "Neurology", Location = "ER_MAIN", CreatedAt = DateTime.Now.AddMinutes(-5) },
                    new ERRequestRow { Id = 103, Specialization = "ER", Location = "ER_NORTH", CreatedAt = DateTime.Now.AddMinutes(-2) }
                };

                foreach (var req in pending)
                {
                    PendingRequests.Add(req);
                    var result = await _dispatchService.DispatchERRequestAsync(req.Id);

                    DispatchResults.Add(new DispatchResultRow
                    {
                        RequestId = result.Request.Id,
                        Specialization = result.Request.Specialization,
                        Location = result.Request.Location,
                        AssignedDoctor = result.MatchedDoctorName ?? "NO MATCH",
                        Success = result.IsSuccess,
                        Message = result.Message
                    });

                    if (!result.IsSuccess)
                        UnmatchedCount++;
                }

                StatusMessage = $"Dispatch complete. {DispatchResults.Count(r => r.Success)} assigned, {UnmatchedCount} unmatched (red flag).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        public sealed class ERRequestRow
        {
            public int Id { get; set; }
            public string Specialization { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        public sealed class DispatchResultRow
        {
            public int RequestId { get; set; }
            public string Specialization { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public string AssignedDoctor { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}

