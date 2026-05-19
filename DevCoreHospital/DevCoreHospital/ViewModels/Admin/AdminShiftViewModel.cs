using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels.Admin
{
    public class AdminShiftViewModel : INotifyPropertyChanged
    {
        private readonly IShiftManagementService staffAndShiftService;
        private const int DAYS_IN_WEEK = 7;
        public ObservableCollection<Shift> Shifts { get; set; } = new ObservableCollection<Shift>();
        public ObservableCollection<IStaff> AvailableStaff { get; set; } = new ObservableCollection<IStaff>();
        public ObservableCollection<string> SpecializationsAndCertifications { get; set; } = new ObservableCollection<string>();

        private DateTime selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => selectedDate;
            set
            {
                if (selectedDate != value)
                {
                    selectedDate = value;
                    OnPropertyChanged(nameof(SelectedDate));
                    LoadAndFilterShifts();
                }
            }
        }

        private string selectedDepartment = "All Departments";
        public string SelectedDepartment
        {
            get => selectedDepartment;
            set
            {
                if (selectedDepartment != value)
                {
                    selectedDepartment = value;
                    OnPropertyChanged(nameof(SelectedDepartment));
                    LoadAndFilterShifts();
                }
            }
        }

        private bool isWeeklyView;
        public bool IsWeeklyView
        {
            get => isWeeklyView;
            set
            {
                if (isWeeklyView != value)
                {
                    isWeeklyView = value;
                    OnPropertyChanged(nameof(IsWeeklyView));
                    LoadAndFilterShifts();
                }
            }
        }

        private string scheduleTitle = string.Empty;
        public string ScheduleTitle
        {
            get => scheduleTitle;
            set
            {
                if (scheduleTitle != value)
                {
                    scheduleTitle = value;
                    OnPropertyChanged(nameof(ScheduleTitle));
                }
            }
        }

        public AdminShiftViewModel(IShiftManagementService service)
        {
            staffAndShiftService = service;
            LoadAndFilterShifts();
        }

        public void LoadAndFilterShifts()
        {
            var rawShifts = staffAndShiftService.GetWeeklyShifts(SelectedDate);
            IEnumerable<Shift> filtered = rawShifts;
            var englishCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

            if (IsWeeklyView)
            {
                int diff = (DAYS_IN_WEEK + (SelectedDate.DayOfWeek - DayOfWeek.Monday)) % DAYS_IN_WEEK;
                DateTime startOfWeek = SelectedDate.Date.AddDays(-diff);
                ScheduleTitle = $"Weekly Roster (Week of {startOfWeek.ToString("dd MMM yyyy", englishCulture)})";
            }
            else
            {
                bool IsShiftOnSelectedDate(Shift shift) => shift.StartTime.Date == SelectedDate.Date;
                filtered = filtered.Where(IsShiftOnSelectedDate);
                ScheduleTitle = $"Daily Roster ({SelectedDate.ToString("dddd, dd MMM yyyy", englishCulture)})";
            }

            if (!string.IsNullOrEmpty(SelectedDepartment) && SelectedDepartment != "All Departments")
            {
                bool IsShiftInSelectedDepartment(Shift shift) => shift.Location == SelectedDepartment;
                filtered = filtered.Where(IsShiftInSelectedDepartment);
            }

            DateTime SortByStartTime(Shift shift) => shift.StartTime;
            Shifts.ReplaceWith(filtered.OrderBy(SortByStartTime));
        }

        public void FilterSpecializationsAndCertificationsForLocation(string location)
        {
            SpecializationsAndCertifications.ReplaceWith(
                staffAndShiftService.GetSpecializationsAndCertificationsForLocation(location));
        }

        public void FilterStaffForShift(string location, string requiredSpecializationOrCertification)
        {
            AvailableStaff.ReplaceWith(
                staffAndShiftService.GetFilteredStaff(location, requiredSpecializationOrCertification));
        }

        public void CreateNewShift(IStaff staff, DateTime start, DateTime end, string location)
        {
            bool isAdded = staffAndShiftService.TryAddShift(staff, start, end, location);
            if (isAdded)
            {
                LoadAndFilterShifts();
            }
        }

        public void SetShiftActive(int shiftID)
        {
            staffAndShiftService.SetShiftActive(shiftID);
            LoadAndFilterShifts();
        }

        public void ReassignShift(Shift shift, IStaff newStaff)
        {
            bool isSuccessful = staffAndShiftService.ReassignShift(shift, newStaff);
            if (isSuccessful)
            {
                LoadAndFilterShifts();
            }
        }

        public void CancelShift(int shiftID)
        {
            staffAndShiftService.CancelShift(shiftID);
            LoadAndFilterShifts();
        }

        public void AutoFindReplacement(Shift shift)
        {
            var replacementsList = staffAndShiftService.FindStaffReplacements(shift);
            if (replacementsList != null && replacementsList.Count > 0)
            {
                ReassignShift(shift, replacementsList.First());
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
