using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class DoctorScheduleViewModel : ObservableObject
    {
        private const int MaxAppointmentsToLoad = 500;

        private readonly ICurrentUserService currentUser;
        private readonly IDoctorAppointmentService appointmentService;
        private readonly IShiftRepository shiftRepository;
        private readonly IDialogService dialogService;

        private int loadVersion;
        private bool isInitializing;

        public ObservableCollection<AppointmentItemViewModel> Appointments { get; } = new ObservableCollection<AppointmentItemViewModel>();
        public ObservableCollection<DoctorShiftItemViewModel> Shifts { get; } = new ObservableCollection<DoctorShiftItemViewModel>();
        public ObservableCollection<DoctorOption> Doctors { get; } = new ObservableCollection<DoctorOption>();

        public enum ScheduleViewMode
        {
            Daily,
            Weekly,
        }

        private ScheduleViewMode viewMode = ScheduleViewMode.Daily;
        public ScheduleViewMode ViewMode
        {
            get => viewMode;
            set
            {
                if (SetProperty(ref viewMode, value))
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

        private DoctorOption? selectedDoctor;
        public DoctorOption? SelectedDoctor
        {
            get => selectedDoctor;
            set
            {
                if (SetProperty(ref selectedDoctor, value) && !isInitializing)
                {
                    _ = LoadAsync();
                }
            }
        }

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set
            {
                if (SetProperty(ref isLoading, value))
                {
                    RaisePropertyChanged(nameof(IsEmpty));
                }
            }
        }

        private string errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => errorMessage;
            set
            {
                if (SetProperty(ref errorMessage, value))
                {
                    RaisePropertyChanged(nameof(IsEmpty));
                }
            }
        }

        private DateTime selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => selectedDate;
            set
            {
                if (SetProperty(ref selectedDate, value))
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

        public bool IsDoctor => string.Equals(currentUser.Role, "Doctor", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
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
            this.currentUser = currentUser;
            this.appointmentService = appointmentService;
            this.shiftRepository = shiftRepository;
            this.dialogService = dialogService;

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
            isInitializing = true;
            IsLoading = true;
            ErrorMessage = string.Empty;
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
                isInitializing = false;
            }

            await LoadAsync();
        }

        private async Task LoadDoctorsAsync()
        {
            Doctors.Clear();

            try
            {
                var allDoctors = await appointmentService.GetAllDoctorsAsync();

                foreach (var doctor in allDoctors.OrderBy(doctor => doctor.DoctorName))
                {
                    Doctors.Add(new DoctorOption
                    {
                        DoctorId = doctor.DoctorId,
                        DoctorName = doctor.DoctorName
                    });
                }

                if (Doctors.Count == 0)
                {
                    ErrorMessage = "No doctors available.";
                    SelectedDoctor = null;
                    return;
                }

                SelectedDoctor = Doctors.FirstOrDefault(doctor => doctor.DoctorId == currentUser.UserId) ?? Doctors.First();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load doctors: {ex.Message}";
                SelectedDoctor = null;
            }
        }

        public async Task LoadAsync()
        {
            int capturedLoadVersion = ++loadVersion;

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
                ErrorMessage = string.Empty;

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

                var rawAppointments = await appointmentService.GetUpcomingAppointmentsAsync(doctorId, from, 0, MaxAppointmentsToLoad);
                var rawShifts = await Task.Run(() => shiftRepository.GetShiftsForStaffInRange(doctorId, from, to));

                if (capturedLoadVersion != loadVersion)
                {
                    return;
                }

                var filteredAppointments = rawAppointments
                    .Where(appointment => appointment.DoctorId == doctorId)
                    .Where(appointment =>
                    {
                        var start = appointment.Date.Date + appointment.StartTime;
                        var end = appointment.Date.Date + appointment.EndTime;
                        if (end <= start)
                        {
                            return false;
                        }

                        return start < to && end > from;
                    })
                    .OrderBy(appointment => appointment.Date)
                    .ThenBy(appointment => appointment.StartTime)
                    .ToList();

                var filteredShifts = rawShifts
                    .Where(shift => shift.Status != ShiftStatus.CANCELLED)
                    .OrderBy(shift => shift.StartTime)
                    .ToList();

                Appointments.Clear();
                foreach (var appointment in filteredAppointments)
                {
                    Appointments.Add(new AppointmentItemViewModel(appointment));
                }

                Shifts.Clear();
                foreach (var shift in filteredShifts)
                {
                    Shifts.Add(new DoctorShiftItemViewModel(shift));
                }
            }
            catch (Exception ex)
            {
                if (capturedLoadVersion == loadVersion)
                {
                    ErrorMessage = $"Failed to load schedule: {ex.Message}";
                }
            }
            finally
            {
                if (capturedLoadVersion == loadVersion)
                {
                    IsLoading = false;
                    RaisePropertyChanged(nameof(IsAccessDenied));
                    RaisePropertyChanged(nameof(IsEmpty));
                }
            }
        }

        public async void OpenDetails(AppointmentItemViewModel? item)
        {
            if (item is null)
            {
                return;
            }

            try
            {
                var appointmentDetails = await appointmentService.GetAppointmentDetailsAsync(item.Id);
                if (appointmentDetails is null)
                {
                    await dialogService.ShowMessageAsync("Details", "Appointment not found.");
                    return;
                }

                var text =
                    $"Patient: {(string.IsNullOrWhiteSpace(item.PatientName) ? "Patient hidden/unknown" : item.PatientName)}\n" +
                    $"Type: {(string.IsNullOrWhiteSpace(appointmentDetails.Type) ? "N/A" : appointmentDetails.Type)}\n" +
                    $"Location: {(string.IsNullOrWhiteSpace(appointmentDetails.Location) ? "Location TBD" : appointmentDetails.Location)}\n" +
                    $"Time: {appointmentDetails.Date:yyyy-MM-dd} {appointmentDetails.StartTime:hh\\:mm}-{appointmentDetails.EndTime:hh\\:mm}\n" +
                    $"Status: {(string.IsNullOrWhiteSpace(appointmentDetails.Status) ? "Unknown" : appointmentDetails.Status)}";

                await dialogService.ShowMessageAsync("Appointment Details", text);
            }
            catch (Exception ex)
            {
                await dialogService.ShowMessageAsync("Details", $"Failed to load details: {ex.Message}");
            }
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            const int daysInWeek = 7;
            var daysFromMonday = (daysInWeek + (date.DayOfWeek - DayOfWeek.Monday)) % daysInWeek;
            return date.Date.AddDays(-1 * daysFromMonday);
        }

        public sealed class DoctorOption
        {
            public int DoctorId { get; set; }
            public string DoctorName { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;

            public string DisplayName =>
                string.Join(" ", new[] { FirstName?.Trim(), LastName?.Trim() }.Where(namePart => !string.IsNullOrWhiteSpace(namePart)));

            public static (string FirstName, string LastName) SplitFirstLast(string? fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    return (string.Empty, string.Empty);
                }

                var parts = fullName
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length == 1)
                {
                    return (parts[0], string.Empty);
                }

                return (parts[0], parts[^1]);
            }
        }
    }
}
