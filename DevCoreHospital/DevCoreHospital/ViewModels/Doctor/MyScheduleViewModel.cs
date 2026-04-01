using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevCoreHospital.ViewModels;
using DoctorModel = DevCoreHospital.Models.Doctor;

namespace DevCoreHospital.ViewModels.Doctor
{
    public sealed class MyScheduleViewModel : INotifyPropertyChanged
    {
        private readonly StaffAndShiftService _staffAndShiftService;
        private readonly ShiftRepository _shiftRepository;
        private readonly StaffRepository _staffRepository;

        public ObservableCollection<DoctorOptionViewModel> Doctors { get; } = new();
        public ObservableCollection<DoctorShiftItemViewModel> FutureShifts { get; } = new();
        public ObservableCollection<StaffOptionViewModel> EligibleColleagues { get; } = new();

        private DoctorOptionViewModel? _selectedDoctor;
        public DoctorOptionViewModel? SelectedDoctor
        {
            get => _selectedDoctor;
            set
            {
                if (SetProperty(ref _selectedDoctor, value))
                {
                    SelectedShift = null;
                    SelectedColleague = null;
                    LoadFutureShifts();
                    ((RelayCommand)RequestSwapCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private DoctorShiftItemViewModel? _selectedShift;
        public DoctorShiftItemViewModel? SelectedShift
        {
            get => _selectedShift;
            set
            {
                if (SetProperty(ref _selectedShift, value))
                {
                    LoadEligibleColleagues();
                    ((RelayCommand)RequestSwapCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private StaffOptionViewModel? _selectedColleague;
        public StaffOptionViewModel? SelectedColleague
        {
            get => _selectedColleague;
            set
            {
                if (SetProperty(ref _selectedColleague, value))
                {
                    ((RelayCommand)RequestSwapCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand RequestSwapCommand { get; }

        public MyScheduleViewModel(
            StaffAndShiftService staffAndShiftService,
            ShiftRepository shiftRepository,
            StaffRepository staffRepository)
        {
            _staffAndShiftService = staffAndShiftService;
            _shiftRepository = shiftRepository;
            _staffRepository = staffRepository;

            RequestSwapCommand = new RelayCommand(RequestSwap, CanRequestSwap);

            LoadDoctors();
        }

        private void LoadDoctors()
        {
            Doctors.Clear();

            // IMPORTANT: load ALL doctors, not only available ones
            var doctors = _staffRepository
                .LoadAllStaff()
                .OfType<DoctorModel>()
                .OrderBy(d => d.FirstName)
                .ThenBy(d => d.LastName)
                .Select(d => new DoctorOptionViewModel
                {
                    StaffId = d.StaffID,
                    DisplayName = $"{d.FirstName} {d.LastName}".Trim()
                });

            foreach (var doctor in doctors)
                Doctors.Add(doctor);

            if (Doctors.Count > 0)
                SelectedDoctor = Doctors[0];
            else
                StatusMessage = "No doctors found in database.";
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

            var data = _shiftRepository
                .GetShiftsByStaffID(SelectedDoctor.StaffId)
                .Where(s => s.StartTime > DateTime.Now)
                .OrderBy(s => s.StartTime)
                .Select(s => new DoctorShiftItemViewModel(s));

            foreach (var item in data)
                FutureShifts.Add(item);

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

            var colleagues = _staffAndShiftService.GetEligibleSwapColleaguesForShift(
                SelectedDoctor.StaffId,
                SelectedShift.Id,
                out var error);

            if (!string.IsNullOrWhiteSpace(error))
            {
                StatusMessage = error;
                return;
            }

            foreach (var c in colleagues)
            {
                EligibleColleagues.Add(new StaffOptionViewModel
                {
                    StaffId = c.StaffID,
                    DisplayName = $"{c.FirstName} {c.LastName}".Trim()
                });
            }

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

            var success = _staffAndShiftService.RequestShiftSwap(
                SelectedDoctor.StaffId,
                SelectedShift.Id,
                SelectedColleague.StaffId,
                out var message);

            StatusMessage = message;

            if (success)
                SelectedColleague = null;
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

    public sealed class DoctorOptionViewModel
    {
        public int StaffId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class StaffOptionViewModel
    {
        public int StaffId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}