using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DevCoreHospital.ViewModels.Admin;
using DevCoreHospital.Models;
using DevCoreHospital.Configuration;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;

namespace DevCoreHospital.Views.Admin
{
    public sealed partial class AdminShiftView : Page
    {
        public AdminShiftViewModel ViewModel { get; set; }

        public AdminShiftView()
        {
            this.InitializeComponent();

            var dbManager = new DevCoreHospital.Data.DatabaseManager(AppSettings.ConnectionString);

            // Va trebui să ai un StaffRepository și un ShiftRepository construite în proiect
            // (Comentează liniile astea dacă vă bazați pe Dependency Injection din App.xaml.cs)
            var staffRepo = new StaffRepository(dbManager);
            var shiftRepo = new ShiftRepository(dbManager);
            var service = new StaffAndShiftService(staffRepo, shiftRepo);

            ViewModel = new AdminShiftViewModel(service);
        }

        private void LocationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // E bine să verificăm dacă ViewModel nu e null (în caz de probleme la instanțiere)
            if (ViewModel == null) return;

            if (LocationComboBox.SelectedItem is string selectedLocation)
            {
                ViewModel.FilterStaffForShift(selectedLocation);
                StaffComboBox.SelectedIndex = -1;
            }
        }

        private void CreateShift_Click(object sender, RoutedEventArgs e)
        {
            if (StaffComboBox.SelectedItem is not IStaff selectedStaff ||
                LocationComboBox.SelectedItem is not string location ||
                !ShiftDatePicker.Date.HasValue ||
                !StartTimePicker.SelectedTime.HasValue ||
                !EndTimePicker.SelectedTime.HasValue)
            {
                ShowMessage("Te rugăm să completezi toate câmpurile formularului!", InfoBarSeverity.Error);
                return;
            }

            DateTime date = ShiftDatePicker.Date.Value.DateTime;
            DateTime start = date.Add(StartTimePicker.SelectedTime.Value);
            DateTime end = date.Add(EndTimePicker.SelectedTime.Value);

            if (end <= start)
            {
                ShowMessage("Eroare: Ora de sfârșit trebuie să fie cronologic după ora de început!", InfoBarSeverity.Warning);
                return;
            }

            ViewModel.CreateNewShift(selectedStaff, start, end, location);

            ShowMessage("Tura a fost programată cu succes!", InfoBarSeverity.Success);

            StaffComboBox.SelectedIndex = -1;
            LocationComboBox.SelectedIndex = -1;
        }

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
            if (sender is Button btn && btn.Tag is Shift shiftToReassign)
            {
                ViewModel.AutoFindReplacement(shiftToReassign);
                ShowMessage("S-a declanșat căutarea automată a unui înlocuitor.", InfoBarSeverity.Success);
            }
        }

        private void ShowMessage(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }
    }
}