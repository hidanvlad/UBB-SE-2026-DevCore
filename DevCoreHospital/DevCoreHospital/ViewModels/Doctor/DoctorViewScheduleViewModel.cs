using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using DevCoreHospital.Views.Shell;

namespace DevCoreHospital.ViewModels.Doctor
{
    public class DoctorScheduleViewModel : ObservableObject
    {
        private const string EnglishCultureCode = "en-US";
        private const string DailyDateFormat = "dddd, dd MMM yyyy";
        private const string WeeklyDateFormat = "dd MMM yyyy";
        private const string AppointmentDateFormat = "yyyy-MM-dd";
        private const string AppointmentTimeFormat = @"hh\:mm";
        private const string DoctorRoleLabel = "Doctor";
        private const string AdminRoleLabel = "Admin";
        private const int DaysInWeek = 7;
        private const int OneDay = 1;

        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo(EnglishCultureCode);

        private readonly ICurrentUserService currentUser;
        private readonly IDoctorAppointmentService appointmentService;
        private readonly DialogPresenter dialogPresenter;

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

        public string SelectedDateText => IsDaily
            ? SelectedDate.ToString(DailyDateFormat, EnglishCulture)
            : $"Week of {StartOfWeek(SelectedDate).ToString(WeeklyDateFormat, EnglishCulture)}";

        public bool IsDoctor => string.Equals(currentUser.Role, DoctorRoleLabel, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(currentUser.Role, AdminRoleLabel, StringComparison.OrdinalIgnoreCase);
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
            DialogPresenter dialogPresenter)
        {
            this.currentUser = currentUser;
            this.appointmentService = appointmentService;
            this.dialogPresenter = dialogPresenter;

            bool CanExecuteAsDoctor() => IsDoctor;
            RefreshCommand = new AsyncRelayCommand(LoadAsync, CanExecuteAsDoctor);

            void SetToday() => SelectedDate = DateTime.Today;
            TodayCommand = new RelayCommand(SetToday, CanExecuteAsDoctor);

            void GoToNextDay() => SelectedDate = IsWeekly ? SelectedDate.AddDays(DaysInWeek) : SelectedDate.AddDays(OneDay);
            NextDayCommand = new RelayCommand(GoToNextDay, CanExecuteAsDoctor);

            void GoToPreviousDay() => SelectedDate = IsWeekly ? SelectedDate.AddDays(-DaysInWeek) : SelectedDate.AddDays(-OneDay);
            PreviousDayCommand = new RelayCommand(GoToPreviousDay, CanExecuteAsDoctor);

            void SetDailyMode() => ViewMode = ScheduleViewMode.Daily;
            DailyModeCommand = new RelayCommand(SetDailyMode, CanExecuteAsDoctor);

            void SetWeeklyMode() => ViewMode = ScheduleViewMode.Weekly;
            WeeklyModeCommand = new RelayCommand(SetWeeklyMode, CanExecuteAsDoctor);
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
            catch (Exception exception)
            {
                ErrorMessage = $"Failed to initialize: {exception.Message}";
            }
            finally
            {
                isInitializing = false;
            }

            await LoadAsync();
        }

        private async Task LoadDoctorsAsync()
        {
            try
            {
                var allDoctors = await appointmentService.GetAllDoctorsAsync();
                Doctors.ReplaceWith(allDoctors.Select(DoctorOption.From));

                if (Doctors.Count == 0)
                {
                    ErrorMessage = "No doctors available.";
                    SelectedDoctor = null;
                    return;
                }

                bool IsCurrentUserDoctor(DoctorOption doctor) => doctor.DoctorId == currentUser.UserId;
                SelectedDoctor = Doctors.FirstOrDefault(IsCurrentUserDoctor) ?? Doctors.FirstOrDefault();
            }
            catch (Exception exception)
            {
                ErrorMessage = $"Failed to load doctors: {exception.Message}";
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

                if (SelectedDoctor is null)
                {
                    Appointments.Clear();
                    Shifts.Clear();
                    IsLoading = false;
                    RaisePropertyChanged(nameof(IsEmpty));
                    return;
                }

                ErrorMessage = string.Empty;

                var doctorId = SelectedDoctor.DoctorId;
                DateTime from = IsDaily ? SelectedDate.Date : StartOfWeek(SelectedDate);
                DateTime to = IsDaily ? from.AddDays(OneDay) : from.AddDays(DaysInWeek);

                var filteredAppointments = await appointmentService.GetAppointmentsInRangeAsync(doctorId, from, to);
                var filteredShifts = await appointmentService.GetShiftsForStaffInRangeAsync(doctorId, from, to);

                if (capturedLoadVersion != loadVersion)
                {
                    return;
                }

                AppointmentItemViewModel ToAppointmentItem(Appointment appointment) => new AppointmentItemViewModel(appointment);
                DoctorShiftItemViewModel ToShiftItem(Shift shift) => new DoctorShiftItemViewModel(shift);

                Appointments.ReplaceWith(filteredAppointments.Select(ToAppointmentItem));
                Shifts.ReplaceWith(filteredShifts.Select(ToShiftItem));
            }
            catch (Exception exception)
            {
                if (capturedLoadVersion == loadVersion)
                {
                    ErrorMessage = $"Failed to load schedule: {exception.Message}";
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
                    await dialogPresenter.ShowMessageAsync("Details", "Appointment not found.");
                    return;
                }

                var patientLine = string.IsNullOrWhiteSpace(item.PatientName) ? "Patient hidden/unknown" : item.PatientName;
                var typeLine = string.IsNullOrWhiteSpace(appointmentDetails.Type) ? "N/A" : appointmentDetails.Type;
                var locationLine = string.IsNullOrWhiteSpace(appointmentDetails.Location) ? "Location TBD" : appointmentDetails.Location;
                var statusLine = string.IsNullOrWhiteSpace(appointmentDetails.Status) ? "Unknown" : appointmentDetails.Status;
                var formattedDate = appointmentDetails.Date.ToString(AppointmentDateFormat);
                var formattedStartTime = appointmentDetails.StartTime.ToString(AppointmentTimeFormat);
                var formattedEndTime = appointmentDetails.EndTime.ToString(AppointmentTimeFormat);

                var text =
                    $"Patient: {patientLine}\n" +
                    $"Type: {typeLine}\n" +
                    $"Location: {locationLine}\n" +
                    $"Time: {formattedDate} {formattedStartTime}-{formattedEndTime}\n" +
                    $"Status: {statusLine}";

                await dialogPresenter.ShowMessageAsync("Appointment Details", text);
            }
            catch (Exception exception)
            {
                await dialogPresenter.ShowMessageAsync("Details", $"Failed to load details: {exception.Message}");
            }
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var daysFromMonday = (DaysInWeek + (date.DayOfWeek - DayOfWeek.Monday)) % DaysInWeek;
            return date.Date.AddDays(-daysFromMonday);
        }

        public sealed class DoctorOption
        {
            private const char NameSeparator = ' ';

            public int DoctorId { get; set; }
            public string DoctorName { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;

            public static DoctorOption From((int DoctorId, string DoctorName) doctor) =>
                new DoctorOption
                {
                    DoctorId = doctor.DoctorId,
                    DoctorName = doctor.DoctorName,
                };

            public string DisplayName
            {
                get
                {
                    bool IsNonEmpty(string? namePart) => !string.IsNullOrWhiteSpace(namePart);
                    return string.Join(NameSeparator, new[] { FirstName?.Trim(), LastName?.Trim() }.Where(IsNonEmpty));
                }
            }

            public static (string FirstName, string LastName) SplitFirstLast(string? fullName)
            {
                const int SingleNamePartCount = 1;
                const int FirstNamePartIndex = 0;

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    return (string.Empty, string.Empty);
                }

                var parts = fullName
                    .Split(NameSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length == SingleNamePartCount)
                {
                    return (parts[FirstNamePartIndex], string.Empty);
                }

                return (parts[FirstNamePartIndex], parts[^SingleNamePartCount]);
            }
        }
    }
}
