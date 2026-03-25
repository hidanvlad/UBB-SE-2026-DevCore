using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;

namespace DevCoreHospital.ViewModels.Doctor;

public class DoctorScheduleViewModel : ObservableObject
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDoctorAppointmentService _appointmentService;

    public ObservableCollection<AppointmentItemViewModel> Appointments { get; } = new();

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

    public Action<int>? OpenAppointmentDetailsRequested { get; set; }

    public DoctorScheduleViewModel(ICurrentUserService currentUser, IDoctorAppointmentService appointmentService)
    {
        _currentUser = currentUser;
        _appointmentService = appointmentService;

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => IsDoctor);
        TodayCommand = new RelayCommand(() => SelectedDate = DateTime.Today, () => IsDoctor);
        NextDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(1), () => IsDoctor);
        PreviousDayCommand = new RelayCommand(() => SelectedDate = SelectedDate.AddDays(-1), () => IsDoctor);
    }

    public async Task InitializeAsync() => await LoadAsync();

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

            var raw = await _appointmentService.GetUpcomingAppointmentsAsync(
                _currentUser.UserId,
                SelectedDate,
                skip: 0,
                take: 300);

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

    public void OpenDetails(AppointmentItemViewModel? item)
    {
        if (item is null) return;
        OpenAppointmentDetailsRequested?.Invoke(item.Id);
    }
}