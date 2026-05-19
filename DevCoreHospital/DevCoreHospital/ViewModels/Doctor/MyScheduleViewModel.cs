using System;
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
    public sealed class MyScheduleViewModel : INotifyPropertyChanged
    {
        private readonly IShiftSwapService staffAndShiftService;

        public ObservableCollection<DoctorOptionViewModel> Doctors { get; } = new ObservableCollection<DoctorOptionViewModel>();
        public ObservableCollection<DoctorShiftItemViewModel> FutureShifts { get; } = new ObservableCollection<DoctorShiftItemViewModel>();
        public ObservableCollection<StaffOptionViewModel> EligibleColleagues { get; } = new ObservableCollection<StaffOptionViewModel>();

        private DoctorOptionViewModel? selectedDoctor;
        public DoctorOptionViewModel? SelectedDoctor
        {
            get => selectedDoctor;
            set
            {
                if (SetProperty(ref selectedDoctor, value))
                {
                    SelectedShift = null;
                    SelectedColleague = null;
                    LoadFutureShifts();
                    ((RelayCommand)RequestSwapCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private DoctorShiftItemViewModel? selectedShift;
        public DoctorShiftItemViewModel? SelectedShift
        {
            get => selectedShift;
            set
            {
                if (SetProperty(ref selectedShift, value))
                {
                    LoadEligibleColleagues();
                    ((RelayCommand)RequestSwapCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private StaffOptionViewModel? selectedColleague;
        public StaffOptionViewModel? SelectedColleague
        {
            get => selectedColleague;
            set
            {
                if (SetProperty(ref selectedColleague, value))
                {
                    ((RelayCommand)RequestSwapCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string statusMessage = string.Empty;
        public string StatusMessage
        {
            get => statusMessage;
            set => SetProperty(ref statusMessage, value);
        }

        public ICommand RequestSwapCommand { get; }

        public MyScheduleViewModel(IShiftSwapService staffAndShiftService)
        {
            this.staffAndShiftService = staffAndShiftService;

            RequestSwapCommand = new RelayCommand(RequestSwap, CanRequestSwap);

            LoadDoctors();
        }

        private void LoadDoctors()
        {
            Doctors.ReplaceWith(staffAndShiftService.GetAllDoctors().Select(DoctorOptionViewModel.From));

            if (Doctors.Count == 0)
            {
                StatusMessage = "No doctors found in database.";
                return;
            }

            SelectedDoctor = Doctors.FirstOrDefault();
        }

        private void LoadFutureShifts()
        {
            FutureShifts.Clear();
            EligibleColleagues.Clear();

            if (SelectedDoctor == null)
            {
                StatusMessage = "Select a doctor first.";
                return;
            }

            DoctorShiftItemViewModel ToShiftItemViewModel(Shift shift) => new DoctorShiftItemViewModel(shift);
            FutureShifts.ReplaceWith(staffAndShiftService
                .GetFutureShiftsForStaff(SelectedDoctor.StaffId)
                .Select(ToShiftItemViewModel));

            StatusMessage = FutureShifts.Count == 0
                ? "Selected doctor has no future shifts available for swap requests."
                : string.Empty;
        }

        private void LoadEligibleColleagues()
        {
            EligibleColleagues.Clear();

            if (SelectedDoctor == null)
            {
                StatusMessage = "Select a doctor first.";
                return;
            }

            if (SelectedShift == null)
            {
                StatusMessage = "Select a future shift first.";
                return;
            }

            var colleagues = staffAndShiftService.GetEligibleSwapColleaguesForShift(
                SelectedDoctor.StaffId,
                SelectedShift.Id,
                out var error);

            if (!string.IsNullOrWhiteSpace(error))
            {
                StatusMessage = error;
                return;
            }

            EligibleColleagues.ReplaceWith(colleagues.Select(StaffOptionViewModel.From));

            StatusMessage = EligibleColleagues.Count == 0
                ? "No colleagues available in the same role/department profile."
                : string.Empty;
        }

        private bool CanRequestSwap()
        {
            return SelectedDoctor != null && SelectedShift != null && SelectedColleague != null;
        }

        private void RequestSwap()
        {
            if (SelectedDoctor == null || SelectedShift == null || SelectedColleague == null)
            {
                StatusMessage = "Please select doctor, shift and colleague.";
                return;
            }

            var success = staffAndShiftService.RequestShiftSwap(
                SelectedDoctor.StaffId,
                SelectedShift.Id,
                SelectedColleague.StaffId,
                out var message);

            StatusMessage = message;

            if (success)
            {
                SelectedColleague = null;
            }
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

    public sealed class DoctorOptionViewModel
    {
        public int StaffId { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public static DoctorOptionViewModel From(Models.Doctor doctor) =>
            new DoctorOptionViewModel
            {
                StaffId = doctor.StaffID,
                DisplayName = $"{doctor.FirstName} {doctor.LastName}".Trim(),
            };
    }

    public sealed class StaffOptionViewModel
    {
        public int StaffId { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public static StaffOptionViewModel From(IStaff staffMember) =>
            new StaffOptionViewModel
            {
                StaffId = staffMember.StaffID,
                DisplayName = $"{staffMember.FirstName} {staffMember.LastName}".Trim(),
            };
    }
}
