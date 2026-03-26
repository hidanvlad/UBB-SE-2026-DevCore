using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Services;

namespace DevCoreHospital.ViewModels.Admin
{
    public class AdminShiftViewModel : INotifyPropertyChanged
    {
        private readonly ShiftService _shiftService;
        public ObservableCollection<Shift> Shifts { get; set; } = new();
        public ObservableCollection<IStaff> AvailableStaff { get; set; } = new();

        public AdminShiftViewModel(ShiftService service)
        {
            _shiftService = service;
            LoadAllShifts();
        }

        private void LoadAllShifts()
        {
            var allShifts = _shiftService.GetWeeklyShifts(DateTime.Now);
            Shifts.Clear();
            foreach (var s in allShifts) Shifts.Add(s);
        }

        // Cerința: Filtrare automată bazată pe locație/specializare
        public void FilterStaffForShift(string location, string requiredSpecialization = "")
        {
            AvailableStaff.Clear();
            var filtered = _shiftService.GetFilteredStaff(location, requiredSpecialization);
            foreach (var staff in filtered)
            {
                AvailableStaff.Add(staff);
            }
        }

        public void CreateNewShift(IStaff staff, DateTime start, DateTime end, string location)
        {
            // Integrity Check: Verificăm suprapunerea (conform tabelului)
            if (_shiftService.ValidateNoOverlap(staff.StaffID, start, end))
            {
                var newShift = new Shift(0, staff, location, start, end, ShiftStatus.SCHEDULED);
                _shiftService.AddShift(newShift);
                Shifts.Add(newShift);
            }
        }

        // --- Metodele din Diagrama UML ---

        public void SetShiftActive(int shiftID)
        {
            _shiftService.SetShiftActive(shiftID);
            // Reîncărcăm lista pentru a vedea statusul nou și availability-ul staff-ului
            LoadAllShifts();
        }

        public void ReassignShift(int shiftID, int newStaffID)
        {
            // Integrity Check se face în interiorul serviciului
            bool success = _shiftService.ReassignShift(shiftID, newStaffID);
            if (success)
            {
                LoadAllShifts();
            }
        }

        public void CancelShift(int shiftID)
        {
            _shiftService.CancelShift(shiftID);
            LoadAllShifts();
        }

        // Metodă Bonus menționată în UML: Găsește automat un înlocuitor valid
        public void AutoFindReplacement(Shift shift)
        {
            var replacement = _shiftService.FindReplacement(shift);
            if (replacement != null)
            {
                ReassignShift(shift.ShiftID, replacement.StaffID);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}