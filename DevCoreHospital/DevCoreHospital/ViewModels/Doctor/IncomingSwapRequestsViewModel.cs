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

        public ObservableCollection<DoctorOptionViewModel> Doctors { get; } = new ObservableCollection<DoctorOptionViewModel>();

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
            : this(shiftSwapService, LoadDoctorsFromService(shiftSwapService))
        {
        }

        private static IEnumerable<DoctorOptionViewModel> LoadDoctorsFromService(IShiftSwapService shiftSwapService)
        {
            string GetFirstName(Models.Doctor doctorModel) => doctorModel.FirstName;
            string GetLastName(Models.Doctor doctorModel) => doctorModel.LastName;
            DoctorOptionViewModel ToDoctorOptionViewModel(Models.Doctor doctorModel) => new DoctorOptionViewModel
            {
                StaffId = doctorModel.StaffID,
                DisplayName = $"{doctorModel.FirstName} {doctorModel.LastName}".Trim(),
            };

            return shiftSwapService.GetAllDoctors()
                .OrderBy(GetFirstName)
                .ThenBy(GetLastName)
                .Select(ToDoctorOptionViewModel);
        }

        public IncomingSwapRequestsViewModel(IShiftSwapService service, IEnumerable<DoctorOptionViewModel> doctors)
        {
            this.service = service;

            foreach (var doctor in doctors)
            {
                Doctors.Add(doctor);
            }

            if (Doctors.Count > 0)
            {
                SelectedDoctor = Doctors[0];
            }

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

            var ok = service.AcceptSwapRequest(SelectedRequest.SwapId, SelectedDoctor.StaffId, out var msg);
            StatusMessage = msg;
            if (ok)
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

            var ok = service.RejectSwapRequest(SelectedRequest.SwapId, SelectedDoctor.StaffId, out var msg);
            StatusMessage = msg;
            if (ok)
            {
                LoadRequests();
            }
        }

        private void LoadRequests()
        {
            Requests.Clear();

            if (SelectedDoctor == null)
            {
                StatusMessage = "Select doctor first.";
                return;
            }

            var list = service.GetIncomingSwapRequests(SelectedDoctor.StaffId);
            foreach (var request in list)
            {
                Requests.Add(new IncomingSwapRequestItemViewModel
                {
                    SwapId = request.SwapId,
                    ShiftId = request.ShiftId,
                    RequesterId = request.RequesterId,
                    RequestedAt = request.RequestedAt,
                    Status = request.Status.ToString(),
                });
            }

            StatusMessage = Requests.Count == 0 ? "No pending requests." : $"{Requests.Count} pending request(s).";
            SelectedRequest = Requests.Count > 0 ? Requests[0] : null;
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
    }
}
