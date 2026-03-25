using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class DoctorScheduleViewModel : ObservableObject
    {
        private readonly ICurrentUserService _currentUser;
        private readonly IDoctorAppointmentService _appointmentService;
        private readonly IDialogService _dialogService;

        public ObservableCollection<AppointmentItemViewModel> Appointments { get; } = new();
        public ObservableCollection<DoctorOption> Doctors { get; } = new();

        private DoctorOption? _selectedDoctor;
        public DoctorOption? SelectedDoctor
        {
            get => _selectedDoctor;
            set
            {
                if (SetProperty(ref _selectedDoctor, value))
                    _ = LoadAsync();
            }
        }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        private string _errorMessage = string.Empty;
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    RaisePropertyChanged(nameof(SelectedDateText));
                    _ = LoadAsync();
                }
            }
        }

        public string SelectedDateText => SelectedDate.ToString("dddd, dd MMM yyyy");
        public bool IsDoctor => string.Equals(_currentUser.Role, "Doctor", StringComparison.OrdinalIgnoreCase);
        public bool IsAccessDenied => !IsDoctor;
        public bool IsEmpty => !IsLoading && string.IsNullOrWhiteSpace(ErrorMessage) && Appointments.Count == 0;

        public AsyncRelayCommand RefreshCommand { get; }
        public RelayCommand TodayCommand { get; }
        public RelayCommand NextDayCommand { get; }
        public RelayCommand PreviousDayCommand { get; }

        public DoctorScheduleViewModel(
            ICurrentUserService currentUser,
            IDoctorAppointmentService appointmentService,
            IDialogService dialogService)
        {
            _currentUser = currentUser;
            _appointmentService = appointmentService;
            _dialogService = dialogService;

            RefreshCommand = new AsyncRelayCommand(LoadAsync, () => IsDoctor);
            TodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today, () => IsDoctor);
            NextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(1), () => IsDoctor);
            PreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1), () => IsDoctor);
        }

        public async Task InitializeAsync()
        {
            await LoadDoctorsAsync();
            await LoadAsync();
        }

        private async Task LoadDoctorsAsync()
        {
            Doctors.Clear();

            var allDoctors = await _appointmentService.GetAllDoctorsAsync();
            foreach (var d in allDoctors)
                Doctors.Add(new DoctorOption { DoctorId = d.DoctorId, DoctorName = d.DoctorName });

            if (Doctors.Count > 0)
                SelectedDoctor = Doctors.First();
        }

        public async Task LoadAsync()
        {
            if (!IsDoctor)
            {
                ErrorMessage = "";
                Appointments.Clear();
                RaisePropertyChanged(nameof(IsAccessDenied));
                RaisePropertyChanged(nameof(IsEmpty));
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = "";
                Appointments.Clear();

                var doctorId = SelectedDoctor?.DoctorId ?? _currentUser.UserId;

                var raw = await _appointmentService.GetUpcomingAppointmentsAsync(
                    doctorId,
                    SelectedDate,
                    0,
                    300);

                foreach (var item in raw.Where(x => x.Date.Date == SelectedDate.Date))
                    Appointments.Add(new AppointmentItemViewModel(item));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load schedule: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                RaisePropertyChanged(nameof(IsAccessDenied));
                RaisePropertyChanged(nameof(IsEmpty));
            }
        }

        public async void OpenDetails(AppointmentItemViewModel? item)
        {
            if (item is null) return;

            try
            {
                var d = await _appointmentService.GetAppointmentDetailsAsync(item.Id);
                if (d is null)
                {
                    await _dialogService.ShowMessageAsync("Details", "Appointment not found.");
                    return;
                }

                var text =
                    $"ID: {d.Id}\n" +
                    $"Doctor ID: {d.DoctorId}\n" +
                    $"Date: {d.Date:yyyy-MM-dd}\n" +
                    $"Time: {d.StartTime:hh\\:mm} - {d.EndTime:hh\\:mm}\n" +
                    $"Status: {d.Status}\n" +
                    $"Type: {d.Type}\n" +
                    $"Location: {d.Location}";

                await _dialogService.ShowMessageAsync("Appointment Details", text);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Details", $"Failed to load details: {ex.Message}");
            }
        }

        public sealed class DoctorOption
        {
            public int DoctorId { get; set; }
            public string DoctorName { get; set; } = string.Empty;
        }
    }
}