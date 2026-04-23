using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System;
using DevCoreHospital.Models;
using DevCoreHospital.Services;

namespace DevCoreHospital.ViewModels.Admin
{
    public class AdminShiftViewModel : INotifyPropertyChanged
    {
        private readonly IShiftManagementService staffAndShiftService;

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
                int diff = (7 + (SelectedDate.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateTime startOfWeek = SelectedDate.Date.AddDays(-1 * diff);
                ScheduleTitle = $"Weekly Roster (Week of {startOfWeek.ToString("dd MMM yyyy", englishCulture)})";
            }
            else
            {
                filtered = filtered.Where(s => s.StartTime.Date == SelectedDate.Date);
                ScheduleTitle = $"Daily Roster ({SelectedDate.ToString("dddd, dd MMM yyyy", englishCulture)})";
            }

            if (!string.IsNullOrEmpty(SelectedDepartment) && SelectedDepartment != "All Departments")
            {
                filtered = filtered.Where(s => s.Location == SelectedDepartment);
            }

            Shifts.Clear();
            var finalResult = filtered.OrderBy(s => s.StartTime).ToList();
            foreach (var s in finalResult)
            {
                Shifts.Add(s);
            }
        }

        public void FilterSpecializationsAndCertificationsForLocation(string location)
        {
            SpecializationsAndCertifications.Clear();
            var list = staffAndShiftService.GetSpecializationsAndCertificationsForLocation(location);
            foreach (var item in list)
            {
                SpecializationsAndCertifications.Add(item);
            }
        }

        public void FilterStaffForShift(string location, string requiredSpecializationOrCertification)
        {
            AvailableStaff.Clear();
            var filtered = staffAndShiftService.GetFilteredStaff(location, requiredSpecializationOrCertification);

            foreach (var staff in filtered)
            {
                AvailableStaff.Add(staff);
            }
        }

        public void CreateNewShift(IStaff staff, DateTime start, DateTime end, string location)
        {
            if (staffAndShiftService.ValidateNoOverlap(staff.StaffID, start, end))
            {
                var newShift = new Shift(0, staff, location, start, end, ShiftStatus.SCHEDULED);
                staffAndShiftService.AddShift(newShift);
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
            bool success = staffAndShiftService.ReassignShift(shift, newStaff);
            if (success)
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