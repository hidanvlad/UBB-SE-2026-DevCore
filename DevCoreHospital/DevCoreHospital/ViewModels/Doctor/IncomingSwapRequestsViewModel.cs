using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels.Doctor
{
    public sealed class IncomingSwapRequestsViewModel : INotifyPropertyChanged
    {
        private readonly IShiftSwapService service;

        public ObservableCollection<IncomingSwapRequestItemViewModel> Requests { get; } = new ObservableCollection<IncomingSwapRequestItemViewModel>();
        public ObservableCollection<DoctorOptionViewModel> Doctors { get; } = new ObservableCollection<DoctorOptionViewModel>();

        private DoctorOptionViewModel? selectedDoctor;
        public DoctorOptionViewModel? SelectedDoctor
        {
            get => selectedDoctor;
            set
            {
                if (SetProperty(ref selectedDoctor, value))
                {
                    LoadRequests();
                }
            }
        }

        private string statusMessage = string.Empty;
        public string StatusMessage
        {
            get => statusMessage;
            set => SetProperty(ref statusMessage, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand AcceptCommand { get; }
        public ICommand RejectCommand { get; }

        public IncomingSwapRequestsViewModel(IShiftSwapService shiftSwapService)
            : this(shiftSwapService, shiftSwapService.GetAllDoctors().Select(DoctorOptionViewModel.From))
        {
        }

        public IncomingSwapRequestsViewModel(IShiftSwapService service, IEnumerable<DoctorOptionViewModel> doctors)
        {
            this.service = service;

            Doctors.ReplaceWith(doctors);
            SelectedDoctor = Doctors.FirstOrDefault();

            bool CanRefresh() => SelectedDoctor != null;
            RefreshCommand = new RelayCommand(LoadRequests, CanRefresh);
            AcceptCommand = new RelayCommand(AcceptSelected, CanProcessSelected);
            RejectCommand = new RelayCommand(RejectSelected, CanProcessSelected);
        }

        private IncomingSwapRequestItemViewModel? selectedRequest;
        public IncomingSwapRequestItemViewModel? SelectedRequest
        {
            get => selectedRequest;
            set
            {
                if (SetProperty(ref selectedRequest, value))
                {
                    (AcceptCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (RejectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool CanProcessSelected() => SelectedDoctor != null && SelectedRequest != null;

        private void AcceptSelected()
        {
            if (SelectedDoctor == null || SelectedRequest == null)
            {
                return;
            }

            var succeeded = service.AcceptSwapRequest(SelectedRequest.SwapId, SelectedDoctor.StaffId, out var resultMessage);
            StatusMessage = resultMessage;
            if (succeeded)
            {
                LoadRequests();
            }
        }

        private void RejectSelected()
        {
            if (SelectedDoctor == null || SelectedRequest == null)
            {
                return;
            }

            var succeeded = service.RejectSwapRequest(SelectedRequest.SwapId, SelectedDoctor.StaffId, out var resultMessage);
            StatusMessage = resultMessage;
            if (succeeded)
            {
                LoadRequests();
            }
        }

        private void LoadRequests()
        {
            if (SelectedDoctor == null)
            {
                Requests.Clear();
                StatusMessage = "Select doctor first.";
                return;
            }

            Requests.ReplaceWith(service.GetIncomingSwapRequests(SelectedDoctor.StaffId)
                .Select(IncomingSwapRequestItemViewModel.From));

            StatusMessage = Requests.Count == 0 ? "No pending requests." : $"{Requests.Count} pending request(s).";
            SelectedRequest = Requests.FirstOrDefault();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(field, value))
            {
                return false;
            }

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

        public static IncomingSwapRequestItemViewModel From(ShiftSwapRequest request) =>
            new IncomingSwapRequestItemViewModel
            {
                SwapId = request.SwapId,
                ShiftId = request.ShiftId,
                RequesterId = request.RequesterId,
                RequestedAt = request.RequestedAt,
                Status = request.Status.ToString(),
            };
    }
}
