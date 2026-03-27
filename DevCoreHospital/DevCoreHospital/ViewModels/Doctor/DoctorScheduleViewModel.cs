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

        private int _loadVersion = 0;
        private bool _isInitializing = false;

        public ObservableCollection<AppointmentItemViewModel> Appointments { get; } = new();
        public ObservableCollection<DoctorOption> Doctors { get; } = new();

        public enum ScheduleViewMode { Daily, Weekly }

        private ScheduleViewMode _viewMode = ScheduleViewMode.Daily;
        public ScheduleViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                if (SetProperty(ref _viewMode, value))
                {
                    RaisePropertyChanged(nameof(IsDaily));
                    RaisePropertyChanged(nameof(IsWeekly));
                    RaisePropertyChanged(nameof(SelectedDateText));
                    _ = LoadAsync();
                }
            }
        }

        public bool IsDaily => ViewMode == ScheduleViewMode.Daily;
        public bool IsWeekly => ViewMode == ScheduleViewMode.Weekly;

        private DoctorOption? _selectedDoctor;
        public DoctorOption? SelectedDoctor
        {
            get => _selectedDoctor;
            set
            {
                if (SetProperty(ref _selectedDoctor, value) && !_isInitializing)
                    _ = LoadAsync();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    RaisePropertyChanged(nameof(IsEmpty));
            }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                    RaisePropertyChanged(nameof(IsEmpty));
            }
        }

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

        public string SelectedDateText =>
            IsDaily
                ? SelectedDate.ToString("dddd, dd MMM yyyy")
                : $"Week of {StartOfWeek(SelectedDate):dd MMM yyyy}";

        public bool IsDoctor => string.Equals(_currentUser.Role, "Doctor", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        public bool IsAccessDenied => !IsDoctor;
        public bool IsEmpty => !IsLoading && string.IsNullOrWhiteSpace(ErrorMessage) && Appointments.Count == 0;

        public AsyncRelayCommand RefreshCommand { get; }
        public RelayCommand TodayCommand { get; }
        public RelayCommand NextDayCommand { get; }
        public RelayCommand PreviousDayCommand { get; }
        public RelayCommand DailyModeCommand { get; }
        public RelayCommand WeeklyModeCommand { get; }

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

            DailyModeCommand = new RelayCommand(() => ViewMode = ScheduleViewMode.Daily, () => IsDoctor);
            WeeklyModeCommand = new RelayCommand(() => ViewMode = ScheduleViewMode.Weekly, () => IsDoctor);
        }

        public async Task InitializeAsync()
        {
            _isInitializing = true;
            IsLoading = true;
            ErrorMessage = "";
            Appointments.Clear();

            try
            {
                await LoadDoctorsAsync();
            }
            finally
            {
                _isInitializing = false;
            }

            await LoadAsync();
        }

        private async Task LoadDoctorsAsync()
        {
            Doctors.Clear();

            var allDoctors = await _appointmentService.GetAllDoctorsAsync();

            foreach (var d in allDoctors.OrderBy(x => x.DoctorName))
            {
                Doctors.Add(new DoctorOption
                {
                    DoctorId = d.DoctorId,
                    DoctorName = d.DoctorName
                });
            }

            if (Doctors.Count == 0)
            {
                ErrorMessage = "No doctors available.";
                SelectedDoctor = null;
                return;
            }

            // default selection = current user if present, otherwise first
            SelectedDoctor = Doctors.FirstOrDefault(d => d.DoctorId == _currentUser.UserId) ?? Doctors.First();
        }

        public async Task LoadAsync()
        {
            int myVersion = ++_loadVersion;

            if (!IsDoctor)
            {
                ErrorMessage = "Access denied. Only doctors can view schedule.";
                Appointments.Clear();
                IsLoading = false;
                RaisePropertyChanged(nameof(IsAccessDenied));
                RaisePropertyChanged(nameof(IsEmpty));
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = "";

                if (SelectedDoctor is null)
                {
                    Appointments.Clear();
                    IsLoading = false;
                    RaisePropertyChanged(nameof(IsEmpty));
                    return;
                }

                var doctorId = SelectedDoctor.DoctorId;
                DateTime from = IsDaily ? SelectedDate.Date : StartOfWeek(SelectedDate);
                DateTime to = IsDaily ? from.AddDays(1) : from.AddDays(7);

                var raw = await _appointmentService.GetUpcomingAppointmentsAsync(doctorId, from, 0, 500);

                if (myVersion != _loadVersion) return;

                var filtered = raw
                    .Where(x => x.DoctorId == doctorId)
                    .Where(x =>
                    {
                        var start = x.Date.Date + x.StartTime;
                        var end = x.Date.Date + x.EndTime;
                        if (end <= start) return false;
                        return start < to && end > from;
                    })
                    .OrderBy(x => x.Date)
                    .ThenBy(x => x.StartTime)
                    .ToList();

                Appointments.Clear();
                foreach (var item in filtered)
                    Appointments.Add(new AppointmentItemViewModel(item));
            }
            catch (Exception ex)
            {
                if (myVersion == _loadVersion)
                    ErrorMessage = $"Failed to load schedule: {ex.Message}";
            }
            finally
            {
                if (myVersion == _loadVersion)
                {
                    IsLoading = false;
                    RaisePropertyChanged(nameof(IsAccessDenied));
                    RaisePropertyChanged(nameof(IsEmpty));
                }
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
                    $"Patient: {(string.IsNullOrWhiteSpace(item.PatientName) ? "Patient hidden/unknown" : item.PatientName)}\n" +
                    $"Type: {(string.IsNullOrWhiteSpace(d.Type) ? "N/A" : d.Type)}\n" +
                    $"Location: {(string.IsNullOrWhiteSpace(d.Location) ? "Location TBD" : d.Location)}\n" +
                    $"Time: {d.Date:yyyy-MM-dd} {d.StartTime:hh\\:mm}-{d.EndTime:hh\\:mm}\n" +
                    $"Status: {(string.IsNullOrWhiteSpace(d.Status) ? "Unknown" : d.Status)}";

                await _dialogService.ShowMessageAsync("Appointment Details", text);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Details", $"Failed to load details: {ex.Message}");
            }
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-1 * diff);
        }

        public sealed class DoctorOption
        {
            public int DoctorId { get; set; }
            public string DoctorName { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;

            public string DisplayName =>
                string.Join(" ", new[] { FirstName?.Trim(), LastName?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));

            public static (string FirstName, string LastName) SplitFirstLast(string? fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName))
                    return (string.Empty, string.Empty);

                var parts = fullName
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length == 1)
                    return (parts[0], string.Empty);

                return (parts[0], parts[^1]);
            }
        }
    }
}