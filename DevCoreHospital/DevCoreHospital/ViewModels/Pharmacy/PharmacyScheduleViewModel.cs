using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels.Pharmacy;

public class PharmacyScheduleViewModel : ObservableObject
{
    private readonly ICurrentUserService currentUser;
    private readonly IPharmacyScheduleService scheduleService;
    private readonly IPharmacyStaffRepository staffRepository;
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

    public string HeaderSubtitle =>
        IsWeeklyView
            ? $"Week of {StartOfWeek(AnchorDate):dd MMM yyyy} \u2013 {StartOfWeek(AnchorDate).AddDays(6):dd MMM yyyy}"
            : AnchorDate.ToString("dddd, dd MMM yyyy");

    public string SelectedDateText =>
        IsWeeklyView
            ? $"Week of {StartOfWeek(AnchorDate):dd MMM yyyy}"
            : AnchorDate.ToString("dddd, dd MMM yyyy");

    public bool IsPharmacist => string.Equals(currentUser.Role, "Pharmacist", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
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
        IPharmacyScheduleService scheduleService,
        IPharmacyStaffRepository staffRepository)
    {
        this.currentUser = currentUser;
        this.scheduleService = scheduleService;
        this.staffRepository = staffRepository;

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => IsPharmacist);
        TodayCommand = new RelayCommand(() => AnchorDate = DateTime.Today, () => IsPharmacist);
        NextPeriodCommand = new RelayCommand(
            () => AnchorDate = IsWeeklyView ? AnchorDate.AddDays(7) : AnchorDate.AddDays(1),
            () => IsPharmacist);
        PreviousPeriodCommand = new RelayCommand(
            () => AnchorDate = IsWeeklyView ? AnchorDate.AddDays(-7) : AnchorDate.AddDays(-1),
            () => IsPharmacist);
        ShowDailyCommand = new RelayCommand(() => IsWeeklyView = false, () => IsPharmacist);
        ShowWeeklyCommand = new RelayCommand(() => IsWeeklyView = true, () => IsPharmacist);
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
        var daysFromMonday = (7 + (int)normalizedDate.DayOfWeek - (int)DayOfWeek.Monday) % 7;
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
            var rangeEnd = IsWeeklyView ? rangeStart.AddDays(7) : rangeStart.AddDays(1);

            var staffId = SelectedPharmacist.StaffId;
            var rawShifts = await scheduleService.GetShiftsAsync(staffId, rangeStart, rangeEnd);

            foreach (var shiftViewModel in rawShifts.Select(rawShift => new PharmacyShiftItemViewModel(rawShift)))
            {
                Shifts.Add(shiftViewModel);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load pharmacy schedule: {ex.Message}";
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
        var allPharmacists = await Task.Run(() => staffRepository.GetPharmacists());

        foreach (var pharmacist in allPharmacists
            .OrderBy(pharmacist => pharmacist.FirstName)
            .ThenBy(pharmacist => pharmacist.LastName))
        {
            Pharmacists.Add(new PharmacistOption
            {
                StaffId = pharmacist.StaffID,
                PharmacistName = string.Join(" ", new[] { pharmacist.FirstName?.Trim(), pharmacist.LastName?.Trim() }
                    .Where(namePart => !string.IsNullOrWhiteSpace(namePart))),
            });
        }

        if (Pharmacists.Count == 0)
        {
            ErrorMessage = "No pharmacists available.";
            SelectedPharmacist = null;
            return;
        }

        SelectedPharmacist = Pharmacists.FirstOrDefault(pharmacist => pharmacist.StaffId == currentUser.UserId)
            ?? Pharmacists.First();
    }

    public sealed class PharmacistOption
    {
        public int StaffId { get; set; }
        public string PharmacistName { get; set; } = string.Empty;
    }
}
