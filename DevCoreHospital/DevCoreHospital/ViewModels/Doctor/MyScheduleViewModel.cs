using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using DoctorModel = DevCoreHospital.Models.Doctor;

namespace DevCoreHospital.ViewModels.Doctor
{
    public sealed class MyScheduleViewModel : INotifyPropertyChanged
    {
        private readonly IShiftSwapService staffAndShiftService;
        private readonly IShiftRepository shiftRepository;
        private readonly IStaffRepository staffRepository;

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

        public MyScheduleViewModel(
            IShiftSwapService staffAndShiftService,
            IShiftRepository shiftRepository,
            IStaffRepository staffRepository)
        {
            this.staffAndShiftService = staffAndShiftService;
            this.shiftRepository = shiftRepository;
            this.staffRepository = staffRepository;

            RequestSwapCommand = new RelayCommand(RequestSwap, CanRequestSwap);

            LoadDoctors();
        }

        private void LoadDoctors()
        {
            Doctors.Clear();

            var doctors = staffRepository
                .LoadAllStaff()
                .OfType<DoctorModel>()
                .OrderBy(doctor => doctor.FirstName)
                .ThenBy(doctor => doctor.LastName)
                .Select(doctor => new DoctorOptionViewModel
                {
                    StaffId = doctor.StaffID,
                    DisplayName = $"{doctor.FirstName} {doctor.LastName}".Trim()
                });

            foreach (var doctor in doctors)
            {
                Doctors.Add(doctor);
            }

            if (Doctors.Count > 0)
            {
                SelectedDoctor = Doctors[0];
            }
            else
            {
                StatusMessage = "No doctors found in database.";
            }
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

            var futureShiftItems = shiftRepository
                .GetShiftsByStaffID(SelectedDoctor.StaffId)
                .Where(shift => shift.StartTime > DateTime.Now)
                .OrderBy(shift => shift.StartTime)
                .Select(shift => new DoctorShiftItemViewModel(shift));

            foreach (var item in futureShiftItems)
            {
                FutureShifts.Add(item);
            }

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

            foreach (var colleague in colleagues)
            {
                EligibleColleagues.Add(new StaffOptionViewModel
                {
                    StaffId = colleague.StaffID,
                    DisplayName = $"{colleague.FirstName} {colleague.LastName}".Trim()
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
    }

    public sealed class StaffOptionViewModel
    {
        public int StaffId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
