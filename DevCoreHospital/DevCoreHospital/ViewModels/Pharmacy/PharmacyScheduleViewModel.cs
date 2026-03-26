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
    private readonly ICurrentUserService _currentUser;
    private readonly IPharmacyScheduleService _scheduleService;
    private readonly IPharmacyHandoverService _handoverService;

    public ObservableCollection<PharmacyShiftItemViewModel> Shifts { get; } = new();
    public ObservableCollection<PharmacyStaffMember> IncomingPharmacists { get; } = new();

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

    private bool _isHandoverBusy;
    public bool IsHandoverBusy
    {
        get => _isHandoverBusy;
        set
        {
            if (SetProperty(ref _isHandoverBusy, value))
                RaisePropertyChanged(nameof(NotHandoverBusy));
        }
    }

    public bool NotHandoverBusy => !IsHandoverBusy;

    private string _errorMessage = string.Empty;
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    private string _handoverErrorMessage = string.Empty;
    public string HandoverErrorMessage { get => _handoverErrorMessage; set => SetProperty(ref _handoverErrorMessage, value); }

    private string _handoverSuccessMessage = string.Empty;
    public string HandoverSuccessMessage { get => _handoverSuccessMessage; set => SetProperty(ref _handoverSuccessMessage, value); }

    private int _processingQueueCount;
    public int ProcessingQueueCount { get => _processingQueueCount; set => SetProperty(ref _processingQueueCount, value); }

    private PharmacyStaffMember? _selectedIncoming;
    public PharmacyStaffMember? SelectedIncoming
    {
        get => _selectedIncoming;
        set
        {
            if (SetProperty(ref _selectedIncoming, value))
            {
                RaisePropertyChanged(nameof(CanCompleteShift));
                CompleteHandoverCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ProcessingQueueText =>
        $"At shift end, orders in Processing (not Completed) for you: {ProcessingQueueCount}";

    public bool CanCompleteShift =>
        IsPharmacist && !IsHandoverBusy && SelectedIncoming != null;

    public bool ShowHandoverSection => IsPharmacist;

    private DateTime _anchorDate = DateTime.Today;
    public DateTime AnchorDate
    {
        get => _anchorDate;
        set
        {
            if (SetProperty(ref _anchorDate, value))
            {
                RaisePropertyChanged(nameof(HeaderSubtitle));
                _ = LoadAsync();
            }
        }
    }

    private bool _isWeeklyView = true;
    public bool IsWeeklyView
    {
        get => _isWeeklyView;
        set
        {
            if (SetProperty(ref _isWeeklyView, value))
            {
                RaisePropertyChanged(nameof(IsDailyView));
                RaisePropertyChanged(nameof(HeaderSubtitle));
                _ = LoadAsync();
            }
        }
    }

    public bool IsDailyView
    {
        get => !_isWeeklyView;
        set => IsWeeklyView = !value;
    }

    public string HeaderSubtitle =>
        IsWeeklyView
            ? $"Week of {StartOfWeek(AnchorDate):dd MMM yyyy} – {(StartOfWeek(AnchorDate).AddDays(6)):dd MMM yyyy}"
            : AnchorDate.ToString("dddd, dd MMM yyyy");

    public bool IsPharmacist => string.Equals(_currentUser.Role, "Pharmacist", StringComparison.OrdinalIgnoreCase);
    public bool IsAccessDenied => !IsPharmacist;
    public bool IsEmpty => !IsLoading && string.IsNullOrWhiteSpace(ErrorMessage) && Shifts.Count == 0;

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand TodayCommand { get; }
    public RelayCommand NextPeriodCommand { get; }
    public RelayCommand PreviousPeriodCommand { get; }
    public RelayCommand ShowDailyCommand { get; }
    public RelayCommand ShowWeeklyCommand { get; }
    public AsyncRelayCommand CompleteHandoverCommand { get; }

    public PharmacyScheduleViewModel(
        ICurrentUserService currentUser,
        IPharmacyScheduleService scheduleService,
        IPharmacyHandoverService handoverService)
    {
        _currentUser = currentUser;
        _scheduleService = scheduleService;
        _handoverService = handoverService;

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
        CompleteHandoverCommand = new AsyncRelayCommand(CompleteHandoverAsync, () => CanCompleteShift);
    }

    public async Task InitializeAsync() => await LoadAsync();

    private static DateTime StartOfWeek(DateTime date)
    {
        var d = date.Date;
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }

    public async Task LoadAsync()
    {
        if (!IsPharmacist)
        {
            ErrorMessage = "";
            Shifts.Clear();
            RaisePropertyChanged(nameof(IsAccessDenied));
            RaisePropertyChanged(nameof(IsEmpty));
            RaisePropertyChanged(nameof(ShowHandoverSection));
            return;
        }

        try
        {
            HandoverSuccessMessage = "";
            IsLoading = true;
            ErrorMessage = "";
            Shifts.Clear();

            var rangeStart = IsWeeklyView ? StartOfWeek(AnchorDate) : AnchorDate.Date;
            var rangeEnd = IsWeeklyView ? rangeStart.AddDays(7) : rangeStart.AddDays(1);

            var staffId = $"PHARM{_currentUser.UserId:D3}";
            var raw = await _scheduleService.GetShiftsAsync(staffId, rangeStart, rangeEnd);

            foreach (var vm in raw.Select(s => new PharmacyShiftItemViewModel(s)))
                Shifts.Add(vm);
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

        await LoadHandoverStateAsync();
    }

    private async Task LoadHandoverStateAsync()
    {
        HandoverErrorMessage = "";
        IncomingPharmacists.Clear();
        SelectedIncoming = null;
        ProcessingQueueCount = 0;
        RaisePropertyChanged(nameof(ProcessingQueueText));
        RaisePropertyChanged(nameof(ShowHandoverSection));

        if (!IsPharmacist)
            return;

        try
        {
            var staffId = _currentUser.UserId;
            ProcessingQueueCount = await _handoverService.GetProcessingQueueCountAsync(staffId);
            RaisePropertyChanged(nameof(ProcessingQueueText));

            var incoming = await _handoverService.GetAvailableIncomingPharmacistsAsync(staffId);
            foreach (var p in incoming)
                IncomingPharmacists.Add(p);
        }
        catch (Exception ex)
        {
            HandoverErrorMessage =
                $"Handover data unavailable ({ex.Message}). Run Data/PharmacyHandover_Setup.sql on your database if tables are missing.";
        }
    }

    private async Task CompleteHandoverAsync()
    {
        if (SelectedIncoming == null)
            return;

        HandoverErrorMessage = "";
        HandoverSuccessMessage = "";

        try
        {
            IsHandoverBusy = true;
            CompleteHandoverCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CanCompleteShift));

            await _handoverService.CompleteShiftHandoverAsync(_currentUser.UserId, SelectedIncoming.StaffId);

            HandoverSuccessMessage =
                "Shift marked COMPLETED. Processing orders were reassigned; your availability was set to unavailable.";
            SelectedIncoming = null;
            await LoadHandoverStateAsync();
        }
        catch (Exception ex)
        {
            HandoverErrorMessage = ex.Message;
        }
        finally
        {
            IsHandoverBusy = false;
            CompleteHandoverCommand.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(CanCompleteShift));
        }
    }
}
