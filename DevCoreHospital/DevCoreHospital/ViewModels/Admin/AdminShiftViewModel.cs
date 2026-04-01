using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using Microsoft.IdentityModel.Tokens;

namespace DevCoreHospital.ViewModels.Admin
{
    public class AdminShiftViewModel : INotifyPropertyChanged
    {
        private readonly StaffAndShiftService _StaffAndShiftService;

        public ObservableCollection<Shift> Shifts { get; set; } = new();
        public ObservableCollection<IStaff> AvailableStaff { get; set; } = new();
        public ObservableCollection<string> SpecializationsAndCertifications { get; set; } = new();

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    OnPropertyChanged(nameof(SelectedDate));
                    LoadAndFilterShifts();
                }
            }
        }

        private string _selectedDepartment = "All Departments";
        public string SelectedDepartment
        {
            get => _selectedDepartment;
            set
            {
                if (_selectedDepartment != value)
                {
                    _selectedDepartment = value;
                    OnPropertyChanged(nameof(SelectedDepartment));
                    LoadAndFilterShifts();
                }
            }
        }

        private bool _isWeeklyView = false;
        public bool IsWeeklyView
        {
            get => _isWeeklyView;
            set
            {
                if (_isWeeklyView != value)
                {
                    _isWeeklyView = value;
                    OnPropertyChanged(nameof(IsWeeklyView));
                    LoadAndFilterShifts();
                }
            }
        }

        private string _scheduleTitle = "";
        public string ScheduleTitle
        {
            get => _scheduleTitle;
            set
            {
                if (_scheduleTitle != value)
                {
                    _scheduleTitle = value;
                    OnPropertyChanged(nameof(ScheduleTitle));
                }
            }
        }

        public AdminShiftViewModel(StaffAndShiftService service)
        {
            _StaffAndShiftService = service;
            LoadAndFilterShifts();
        }
        

        public void LoadAndFilterShifts()
        {
            var rawShifts = _StaffAndShiftService.GetWeeklyShifts(SelectedDate);
            IEnumerable<Shift> filtered = rawShifts;

            if (IsWeeklyView)
            {
                int diff = (7 + (SelectedDate.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateTime startOfWeek = SelectedDate.Date.AddDays(-1 * diff);
                ScheduleTitle = $"Weekly Roster (Week of {startOfWeek:dd MMM yyyy})";
            }
            else
            {
                filtered = filtered.Where(s => s.StartTime.Date == SelectedDate.Date);
                ScheduleTitle = $"Daily Roster ({SelectedDate:dddd, dd MMM yyyy})";
            }

            if (!string.IsNullOrEmpty(SelectedDepartment) && SelectedDepartment != "All Departments")
            {
                filtered = filtered.Where(s => s.Location == SelectedDepartment);
            }


            Shifts.Clear();
            var finalResult = filtered.OrderBy(s => s.StartTime).ToList();
            foreach (var s in finalResult) Shifts.Add(s);
        }


        
        public void FilterSpecializationsAndCertificationsForLocation(string location)
        {
            SpecializationsAndCertifications.Clear();
            var list = _StaffAndShiftService.GetSpecializationsAndCertificationsForLocation(location);
            foreach (var item in list)
            {
                SpecializationsAndCertifications.Add(item);
            }
        }

        public void FilterStaffForShift(string location, string requiredSpecializationOrCertification)
        {
            AvailableStaff.Clear();
            var filtered = _StaffAndShiftService.GetFilteredStaff(location, requiredSpecializationOrCertification);

            foreach (var staff in filtered)
            {
                AvailableStaff.Add(staff);
            }
        }

        public void CreateNewShift(IStaff staff, DateTime start, DateTime end, string location)
        {
            if (_StaffAndShiftService.ValidateNoOverlap(staff.StaffID, start, end))
            {
                var newShift = new Shift(0, staff, location, start, end, ShiftStatus.SCHEDULED);
                _StaffAndShiftService.AddShift(newShift);
                LoadAndFilterShifts();
            }
        }

        public void SetShiftActive(int shiftID)
        {
            _StaffAndShiftService.SetShiftActive(shiftID);
            LoadAndFilterShifts();
        }

        public void ReassignShift(Shift shift, IStaff newStaff)
        {
            bool success = _StaffAndShiftService.ReassignShift(shift, newStaff);
            if (success)
            {
                LoadAndFilterShifts();
            }
        }

        public void CancelShift(int shiftID)
        {
            _StaffAndShiftService.CancelShift(shiftID);
            LoadAndFilterShifts();
        }

        public void AutoFindReplacement(Shift shift)
        {
            var replacementsList = _StaffAndShiftService.FindStaffReplacements(shift);
            if (!replacementsList.IsNullOrEmpty())
            {
                ReassignShift(shift, replacementsList.First());
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}