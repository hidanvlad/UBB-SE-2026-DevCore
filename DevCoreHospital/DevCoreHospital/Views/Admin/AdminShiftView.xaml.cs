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

            var staffRepo = new StaffRepository(dbManager);
            var shiftRepo = new ShiftRepository(dbManager);
            var service = new StaffAndShiftService(staffRepo, shiftRepo, dbManager);

            ViewModel = new AdminShiftViewModel(service);
        }

        private void LocationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null) return;

            if (LocationComboBox.SelectedItem is string selectedLocation)
            {
                ViewModel.FilterSpecializationsAndCertificationsForLocation(selectedLocation);
            }
        }

        private void SpecializationCertificationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null) return;

            var selectedLocation = LocationComboBox.SelectedItem as string;
            var selectedSpecializationOrCertification = SpecializationCertificationComboBox.SelectedItem as string;

            if (!string.IsNullOrEmpty(selectedSpecializationOrCertification) && !string.IsNullOrEmpty(selectedLocation))
            {
                ViewModel.FilterStaffForShift(selectedLocation, selectedSpecializationOrCertification);
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
                ShowMessage("Please fill all the fields of the form!", InfoBarSeverity.Error);
                return;
            }

            DateTime date = ShiftDatePicker.Date.Value.Date;
            DateTime start = date.Add(StartTimePicker.SelectedTime.Value);
            DateTime end = date.Add(EndTimePicker.SelectedTime.Value);

            if (end <= start)
            {
                ShowMessage("Error: End hour must be chronologically after the start hour!", InfoBarSeverity.Warning);
                return;
            }

            ViewModel.CreateNewShift(selectedStaff, start, end, location);

            ShowMessage("The shift was scheduled successfuly!", InfoBarSeverity.Success);

            StaffComboBox.SelectedIndex = -1;
            LocationComboBox.SelectedIndex = -1;
        }

        private void SetActive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int shiftId)
            {
                ViewModel.SetShiftActive(shiftId);
                ShowMessage($"The shift #{shiftId} was marked as active.", InfoBarSeverity.Success);
            }
        }

        private void CancelShift_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int shiftId)
            {
                ViewModel.CancelShift(shiftId);
                ShowMessage($"The shift #{shiftId} was cancelled.", InfoBarSeverity.Informational);
            }
        }

        private void AutoReassign_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Shift shiftToReassign)
            {
                ViewModel.AutoFindReplacement(shiftToReassign);
                ShowMessage("The automatic searching of a replacement has been triggered.", InfoBarSeverity.Success);
            }
        }

        private void OpenSchedule_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(AdminSchedulePage));
        }

        private void ShowMessage(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }
    }
}