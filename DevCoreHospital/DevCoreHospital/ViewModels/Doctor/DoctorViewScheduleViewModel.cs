using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class DoctorScheduleViewModel : ObservableObject
    {
        private readonly ICurrentUserService _currentUser;
        private readonly IDoctorAppointmentService _appointmentService;
        private readonly IShiftRepository _shiftRepository;
        private readonly IDialogService _dialogService;

        private int _loadVersion = 0;
        private bool _isInitializing = false;

        public ObservableCollection<AppointmentItemViewModel> Appointments { get; } = new();
        public ObservableCollection<DoctorShiftItemViewModel> Shifts { get; } = new();
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
                    RaisePropertyChanged(nameof(PreviousButtonText));
                    RaisePropertyChanged(nameof(NextButtonText));
                    _ = LoadAsync();
                }
            }
        }

        public bool IsDaily => ViewMode == ScheduleViewMode.Daily;
        public bool IsWeekly => ViewMode == ScheduleViewMode.Weekly;

        public string PreviousButtonText => IsWeekly ? "Previous Week" : "Previous";
        public string NextButtonText => IsWeekly ? "Next Week" : "Next";

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

        public string SelectedDateText
        {
            get
            {
                var englishCulture = CultureInfo.GetCultureInfo("en-US");
                return IsDaily
                    ? SelectedDate.ToString("dddd, dd MMM yyyy", englishCulture)
                    : $"Week of {StartOfWeek(SelectedDate).ToString("dd MMM yyyy", englishCulture)}";
            }
        }

        public bool IsDoctor => string.Equals(_currentUser.Role, "Doctor", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        public bool IsAccessDenied => !IsDoctor;
        public bool IsEmpty => !IsLoading && string.IsNullOrWhiteSpace(ErrorMessage) && Appointments.Count == 0 && Shifts.Count == 0;

        public AsyncRelayCommand RefreshCommand { get; }
        public RelayCommand TodayCommand { get; }
        public RelayCommand NextDayCommand { get; }
        public RelayCommand PreviousDayCommand { get; }
        public RelayCommand DailyModeCommand { get; }
        public RelayCommand WeeklyModeCommand { get; }

        public DoctorScheduleViewModel(
            ICurrentUserService currentUser,
            IDoctorAppointmentService appointmentService,
            IShiftRepository shiftRepository,
            IDialogService dialogService)
        {
            _currentUser = currentUser;
            _appointmentService = appointmentService;
            _shiftRepository = shiftRepository;
            _dialogService = dialogService;

            RefreshCommand = new AsyncRelayCommand(LoadAsync, () => IsDoctor);
            TodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today, () => IsDoctor);

            NextDayCommand = new RelayCommand(
                () => SelectedDate = IsWeekly ? SelectedDate.AddDays(7) : SelectedDate.AddDays(1),
                () => IsDoctor);

            PreviousDayCommand = new RelayCommand(
                () => SelectedDate = IsWeekly ? SelectedDate.AddDays(-7) : SelectedDate.AddDays(-1),
                () => IsDoctor);

            DailyModeCommand = new RelayCommand(() => ViewMode = ScheduleViewMode.Daily, () => IsDoctor);
            WeeklyModeCommand = new RelayCommand(() => ViewMode = ScheduleViewMode.Weekly, () => IsDoctor);
        }

        public async Task InitializeAsync()
        {
            _isInitializing = true;
            IsLoading = true;
            ErrorMessage = "";
            Appointments.Clear();
            Shifts.Clear();

            try
            {
                await LoadDoctorsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to initialize: {ex.Message}";
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

            try
            {
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

                SelectedDoctor = Doctors.FirstOrDefault(d => d.DoctorId == _currentUser.UserId) ?? Doctors.First();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load doctors: {ex.Message}";
                SelectedDoctor = null;
            }
        }

        public async Task LoadAsync()
        {
            int myVersion = ++_loadVersion;

            if (!IsDoctor)
            {
                ErrorMessage = "Access denied. Only doctors can view schedule.";
                Appointments.Clear();
                Shifts.Clear();
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
                    Shifts.Clear();
                    IsLoading = false;
                    RaisePropertyChanged(nameof(IsEmpty));
                    return;
                }

                var doctorId = SelectedDoctor.DoctorId;
                DateTime from = IsDaily ? SelectedDate.Date : StartOfWeek(SelectedDate);
                DateTime to = IsDaily ? from.AddDays(1) : from.AddDays(7);

                var rawAppointments = await _appointmentService.GetUpcomingAppointmentsAsync(doctorId, from, 0, 500);
                var rawShifts = await Task.Run(() => _shiftRepository.GetShiftsForStaffInRange(doctorId, from, to));

                if (myVersion != _loadVersion) return;

                var filteredAppointments = rawAppointments
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

                var filteredShifts = rawShifts
                    .Where(x => x.Status != ShiftStatus.CANCELLED)
                    .OrderBy(x => x.StartTime)
                    .ToList();

                Appointments.Clear();
                foreach (var item in filteredAppointments)
                    Appointments.Add(new AppointmentItemViewModel(item));

                Shifts.Clear();
                foreach (var shift in filteredShifts)
                    Shifts.Add(new DoctorShiftItemViewModel(shift));
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