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

        public AdminShiftViewModel(StaffAndShiftService service)
        {
            _StaffAndShiftService = service;
            LoadAllShifts();
        }

        private void LoadAllShifts()
        {
            var allShifts = _StaffAndShiftService.GetWeeklyShifts(DateTime.Now);
            Shifts.Clear();
            foreach (var s in allShifts) Shifts.Add(s);
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
            // Integrity Check: Verificăm suprapunerea (conform tabelului)
            if (_StaffAndShiftService.ValidateNoOverlap(staff.StaffID, start, end))
            {
                var newShift = new Shift(0, staff, location, start, end, ShiftStatus.SCHEDULED);
                _StaffAndShiftService.AddShift(newShift);
                Shifts.Add(newShift);
            }
        }

        // --- Metodele din Diagrama UML ---

        public void SetShiftActive(int shiftID)
        {
            _StaffAndShiftService.SetShiftActive(shiftID);
            // Reîncărcăm lista pentru a vedea statusul nou și availability-ul staff-ului
            LoadAllShifts();
        }

        public void ReassignShift(Shift shift, IStaff newStaff)
        {
            // Integrity Check se face în interiorul serviciului
            bool success = _StaffAndShiftService.ReassignShift(shift, newStaff);
            if (success)
            {
                LoadAllShifts();
            }
        }

        public void CancelShift(int shiftID)
        {
            _StaffAndShiftService.CancelShift(shiftID);
            LoadAllShifts();
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