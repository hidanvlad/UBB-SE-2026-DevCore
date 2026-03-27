using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DevCoreHospital.ViewModels.Admin;
using DevCoreHospital.Models;

namespace DevCoreHospital.Views.Admin
{
    public sealed partial class AdminShiftView : Page
    {
        // Proprietatea pentru Data Binding (x:Bind)
        public AdminShiftViewModel ViewModel { get; }

        public AdminShiftView()
        {
            this.InitializeComponent();
            
            // ATENȚIE: Aici trebuie să injectezi ViewModel-ul. 
            // Dacă nu aveți un sistem de Dependency Injection configurat încă, 
            // va trebui să îl instanțiezi manual (ex: ViewModel = new AdminShiftViewModel(new StaffAndShiftService(...));)
            // ViewModel = App.GetService<AdminShiftViewModel>(); 
        }

        // --- 1. Filtrare Personal la schimbarea locației ---
        private void LocationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LocationComboBox.SelectedItem is string selectedLocation)
            {
                // Apelează metoda din ViewModel. Lista și ComboBox-ul se vor updata automat.
                ViewModel.FilterStaffForShift(selectedLocation);
                StaffComboBox.SelectedIndex = -1; // Resetăm selecția veche
            }
        }

        // --- 2. Creare Tură Nouă ---
        private void CreateShift_Click(object sender, RoutedEventArgs e)
        {
            // Validare simplă UI
            if (StaffComboBox.SelectedItem is not IStaff selectedStaff ||
                LocationComboBox.SelectedItem is not string location ||
                !ShiftDatePicker.Date.HasValue ||
                !StartTimePicker.SelectedTime.HasValue ||
                !EndTimePicker.SelectedTime.HasValue)
            {
                ShowMessage("Te rugăm să completezi toate câmpurile formularului!", InfoBarSeverity.Error);
                return;
            }

            // Construirea datelor calendaristice
            DateTime date = ShiftDatePicker.Date.Value.DateTime;
            DateTime start = date.Add(StartTimePicker.SelectedTime.Value);
            DateTime end = date.Add(EndTimePicker.SelectedTime.Value);

            if (end <= start)
            {
                ShowMessage("Eroare: Ora de sfârșit trebuie să fie cronologic după ora de început!", InfoBarSeverity.Warning);
                return;
            }

            // Trimiterea datelor către ViewModel
            ViewModel.CreateNewShift(selectedStaff, start, end, location);
            
            ShowMessage("Tura a fost programată cu succes!", InfoBarSeverity.Success);
            
            // Resetare UI după salvare
            StaffComboBox.SelectedIndex = -1;
            LocationComboBox.SelectedIndex = -1;
        }

        // --- 3. Management Ture (Din Lista din Dreapta) ---

        private void SetActive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int shiftId)
            {
                ViewModel.SetShiftActive(shiftId);
                ShowMessage($"Tura #{shiftId} a fost marcată ca activă.", InfoBarSeverity.Success);
            }
        }

        private void CancelShift_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int shiftId)
            {
                ViewModel.CancelShift(shiftId);
                ShowMessage($"Tura #{shiftId} a fost anulată.", InfoBarSeverity.Informational);
            }
        }

        private void AutoReassign_Click(object sender, RoutedEventArgs e)
        {
            // Preluăm obiectul întreg "Shift" pentru a-l pasa metodei tale
            if (sender is Button btn && btn.Tag is Shift shiftToReassign)
            {
                ViewModel.AutoFindReplacement(shiftToReassign);
                ShowMessage("S-a declanșat căutarea automată a unui înlocuitor.", InfoBarSeverity.Success);
            }
        }

        // --- Helper pentru Feedback UI ---
        private void ShowMessage(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }
    }
}