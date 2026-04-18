using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DevCoreHospital.ViewModels.Doctor
{
    public sealed class IncomingSwapRequestsViewModel : INotifyPropertyChanged
    {
        private readonly IStaffAndShiftService _service;

        public ObservableCollection<IncomingSwapRequestItemViewModel> Requests { get; } = new();

        private DoctorOptionViewModel? _selectedDoctor;
        public DoctorOptionViewModel? SelectedDoctor
        {
            get => _selectedDoctor;
            set
            {
                if (SetProperty(ref _selectedDoctor, value))
                    LoadRequests();
            }
        }

        public ObservableCollection<DoctorOptionViewModel> Doctors { get; } = new();

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand AcceptCommand { get; }
        public ICommand RejectCommand { get; }

        public IncomingSwapRequestsViewModel(IStaffAndShiftService service, System.Collections.Generic.IEnumerable<DoctorOptionViewModel> doctors)
        {
            _service = service;

            foreach (var d in doctors) Doctors.Add(d);
            if (Doctors.Count > 0) SelectedDoctor = Doctors[0];

            RefreshCommand = new RelayCommand(LoadRequests, () => SelectedDoctor != null);
            AcceptCommand = new RelayCommand(AcceptSelected, CanProcessSelected);
            RejectCommand = new RelayCommand(RejectSelected, CanProcessSelected);
        }

        private IncomingSwapRequestItemViewModel? _selectedRequest;
        public IncomingSwapRequestItemViewModel? SelectedRequest
        {
            get => _selectedRequest;
            set
            {
                if (SetProperty(ref _selectedRequest, value))
                {
                    (AcceptCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (RejectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool CanProcessSelected() => SelectedDoctor != null && SelectedRequest != null;

        private void AcceptSelected()
        {
            if (SelectedDoctor == null || SelectedRequest == null) return;
            var ok = _service.AcceptSwapRequest(SelectedRequest.SwapId, SelectedDoctor.StaffId, out var msg);
            StatusMessage = msg;
            if (ok) LoadRequests();
        }

        private void RejectSelected()
        {
            if (SelectedDoctor == null || SelectedRequest == null) return;
            var ok = _service.RejectSwapRequest(SelectedRequest.SwapId, SelectedDoctor.StaffId, out var msg);
            StatusMessage = msg;
            if (ok) LoadRequests();
        }

        private void LoadRequests()
        {
            Requests.Clear();

            if (SelectedDoctor == null)
            {
                StatusMessage = "Select doctor first.";
                return;
            }

            var list = _service.GetIncomingSwapRequests(SelectedDoctor.StaffId);
            foreach (var r in list)
            {
                Requests.Add(new IncomingSwapRequestItemViewModel
                {
                    SwapId = r.SwapId,
                    ShiftId = r.ShiftId,
                    RequesterId = r.RequesterId,
                    RequestedAt = r.RequestedAt,
                    Status = r.Status.ToString()
                });
            }

            StatusMessage = Requests.Count == 0 ? "No pending requests." : $"{Requests.Count} pending request(s).";
            SelectedRequest = Requests.Count > 0 ? Requests[0] : null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public sealed class IncomingSwapRequestItemViewModel
    {
        public int SwapId { get; set; }
        public int ShiftId { get; set; }
        public int RequesterId { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string DisplayText => $"Request #{SwapId} | Shift #{ShiftId} | From staff #{RequesterId} | {RequestedAt:g}";
    }
}