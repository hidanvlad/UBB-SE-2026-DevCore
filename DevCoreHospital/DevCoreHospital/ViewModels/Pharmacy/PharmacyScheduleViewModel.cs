using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels.Pharmacy;

public class PharmacyScheduleViewModel : ObservableObject
{
    private const string PharmacistRoleLabel = "Pharmacist";
    private const string AdminRoleLabel = "Admin";
    private const string DailyDateFormat = "dddd, dd MMM yyyy";
    private const string WeeklyDateFormat = "dd MMM yyyy";
    private const int DaysInWeek = 7;
    private const int OneDay = 1;

    private readonly ICurrentUserService currentUser;
    private readonly IPharmacyScheduleService scheduleService;
    private bool isInitializing;

    public ObservableCollection<PharmacyShiftItemViewModel> Shifts { get; } = new ObservableCollection<PharmacyShiftItemViewModel>();
    public ObservableCollection<PharmacistOption> Pharmacists { get; } = new ObservableCollection<PharmacistOption>();

    private PharmacistOption? selectedPharmacist;
    public PharmacistOption? SelectedPharmacist
    {
        get => selectedPharmacist;
        set
        {
            if (SetProperty(ref selectedPharmacist, value) && !isInitializing)
            {
                _ = LoadAsync();
            }
        }
    }

    private bool isLoading;
    public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }

    private string errorMessage = string.Empty;
    public string ErrorMessage { get => errorMessage; set => SetProperty(ref errorMessage, value); }

    private DateTime anchorDate = DateTime.Today;
    public DateTime AnchorDate
    {
        get => anchorDate;
        set
        {
            if (SetProperty(ref anchorDate, value))
            {
                RaisePropertyChanged(nameof(HeaderSubtitle));
                RaisePropertyChanged(nameof(SelectedDateText));
                _ = LoadAsync();
            }
        }
    }

    private bool isWeeklyView = true;
    public bool IsWeeklyView
    {
        get => isWeeklyView;
        set
        {
            if (SetProperty(ref isWeeklyView, value))
            {
                RaisePropertyChanged(nameof(IsDailyView));
                RaisePropertyChanged(nameof(HeaderSubtitle));
                RaisePropertyChanged(nameof(SelectedDateText));
                _ = LoadAsync();
            }
        }
    }

    public bool IsDailyView
    {
        get => !isWeeklyView;
        set => IsWeeklyView = !value;
    }

    public string HeaderSubtitle
    {
        get
        {
            const int LastDayOfWeekOffset = DaysInWeek - 1;
            return IsWeeklyView
                ? $"Week of {StartOfWeek(AnchorDate).ToString(WeeklyDateFormat)} – {StartOfWeek(AnchorDate).AddDays(LastDayOfWeekOffset).ToString(WeeklyDateFormat)}"
                : AnchorDate.ToString(DailyDateFormat);
        }
    }

    public string SelectedDateText =>
        IsWeeklyView
            ? $"Week of {StartOfWeek(AnchorDate).ToString(WeeklyDateFormat)}"
            : AnchorDate.ToString(DailyDateFormat);

    public bool IsPharmacist => string.Equals(currentUser.Role, PharmacistRoleLabel, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(currentUser.Role, AdminRoleLabel, StringComparison.OrdinalIgnoreCase);
    public bool IsAccessDenied => !IsPharmacist;
    public bool IsEmpty => !IsLoading && string.IsNullOrWhiteSpace(ErrorMessage) && Shifts.Count == 0;

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand TodayCommand { get; }
    public RelayCommand NextPeriodCommand { get; }
    public RelayCommand PreviousPeriodCommand { get; }
    public RelayCommand ShowDailyCommand { get; }
    public RelayCommand ShowWeeklyCommand { get; }

    public PharmacyScheduleViewModel(
        ICurrentUserService currentUser,
        IPharmacyScheduleService scheduleService)
    {
        this.currentUser = currentUser;
        this.scheduleService = scheduleService;

        bool CanExecuteAsPharmacist() => IsPharmacist;
        RefreshCommand = new AsyncRelayCommand(LoadAsync, CanExecuteAsPharmacist);

        void SetToday() => AnchorDate = DateTime.Today;
        TodayCommand = new RelayCommand(SetToday, CanExecuteAsPharmacist);

        void GoToNextPeriod() => AnchorDate = IsWeeklyView ? AnchorDate.AddDays(DaysInWeek) : AnchorDate.AddDays(OneDay);
        NextPeriodCommand = new RelayCommand(GoToNextPeriod, CanExecuteAsPharmacist);

        void GoToPreviousPeriod() => AnchorDate = IsWeeklyView ? AnchorDate.AddDays(-DaysInWeek) : AnchorDate.AddDays(-OneDay);
        PreviousPeriodCommand = new RelayCommand(GoToPreviousPeriod, CanExecuteAsPharmacist);

        void ShowDaily() => IsWeeklyView = false;
        ShowDailyCommand = new RelayCommand(ShowDaily, CanExecuteAsPharmacist);

        void ShowWeekly() => IsWeeklyView = true;
        ShowWeeklyCommand = new RelayCommand(ShowWeekly, CanExecuteAsPharmacist);
    }

    public async Task InitializeAsync()
    {
        isInitializing = true;
        try
        {
            await LoadPharmacistsAsync();
        }
        finally
        {
            isInitializing = false;
        }

        await LoadAsync();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var normalizedDate = date.Date;
        var daysFromMonday = (DaysInWeek + (int)normalizedDate.DayOfWeek - (int)DayOfWeek.Monday) % DaysInWeek;
        return normalizedDate.AddDays(-daysFromMonday);
    }

    public async Task LoadAsync()
    {
        if (!IsPharmacist)
        {
            ErrorMessage = string.Empty;
            Shifts.Clear();
            RaisePropertyChanged(nameof(IsAccessDenied));
            RaisePropertyChanged(nameof(IsEmpty));
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            Shifts.Clear();

            if (SelectedPharmacist is null)
            {
                IsLoading = false;
                RaisePropertyChanged(nameof(IsEmpty));
                return;
            }

            var rangeStart = IsWeeklyView ? StartOfWeek(AnchorDate) : AnchorDate.Date;
            var rangeEnd = IsWeeklyView ? rangeStart.AddDays(DaysInWeek) : rangeStart.AddDays(OneDay);

            var staffId = SelectedPharmacist.StaffId;
            var rawShifts = await scheduleService.GetShiftsAsync(staffId, rangeStart, rangeEnd);

            PharmacyShiftItemViewModel ToShiftViewModel(Shift rawShift) => new PharmacyShiftItemViewModel(rawShift);
            foreach (var shiftViewModel in rawShifts.Select(ToShiftViewModel))
            {
                Shifts.Add(shiftViewModel);
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Failed to load pharmacy schedule: {exception.Message}";
        }
        finally
        {
            IsLoading = false;
            RaisePropertyChanged(nameof(IsAccessDenied));
            RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    private async Task LoadPharmacistsAsync()
    {
        Pharmacists.Clear();
        var allPharmacists = await Task.Run(() => scheduleService.GetPharmacists());

        string GetPharmacistFirstName(Pharmacyst pharmacist) => pharmacist.FirstName;
        string GetPharmacistLastName(Pharmacyst pharmacist) => pharmacist.LastName;
        bool IsNonEmpty(string? namePart) => !string.IsNullOrWhiteSpace(namePart);

        foreach (var pharmacist in allPharmacists
            .OrderBy(GetPharmacistFirstName)
            .ThenBy(GetPharmacistLastName))
        {
            Pharmacists.Add(new PharmacistOption
            {
                StaffId = pharmacist.StaffID,
                PharmacistName = string.Join(" ", new[] { pharmacist.FirstName?.Trim(), pharmacist.LastName?.Trim() }
                    .Where(IsNonEmpty)),
            });
        }

        if (Pharmacists.Count == 0)
        {
            ErrorMessage = "No pharmacists available.";
            SelectedPharmacist = null;
            return;
        }

        bool IsCurrentUser(PharmacistOption pharmacist) => pharmacist.StaffId == currentUser.UserId;
        SelectedPharmacist = Pharmacists.FirstOrDefault(IsCurrentUser)
            ?? Pharmacists.First();
    }

    public sealed class PharmacistOption
    {
        public int StaffId { get; set; }
        public string PharmacistName { get; set; } = string.Empty;
    }
}
