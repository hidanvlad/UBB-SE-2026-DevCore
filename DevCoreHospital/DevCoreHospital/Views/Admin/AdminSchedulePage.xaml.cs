using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Admin;
using DevCoreHospital.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace DevCoreHospital.Views.Admin
{
    public sealed partial class AdminSchedulePage : Page
    {
        public AdminShiftViewModel ViewModel { get; }
        private bool _initialized;

        public AdminSchedulePage()
        {
            this.InitializeComponent();

            var dbManager = new DatabaseManager(AppSettings.ConnectionString);
            var shiftRepo = new ShiftRepository(dbManager);
            var staffRepo = new StaffRepository(dbManager);

            var service = new StaffAndShiftService(staffRepo, shiftRepo, dbManager);
            ViewModel = new AdminShiftViewModel(service);

            DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (_initialized) return;
            _initialized = true;

            ViewModel.LoadAndFilterShifts();
            DateCalendar.SelectedDates.Add(DateTime.Today);
        }

        private void DateCalendar_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (sender.SelectedDates == null || sender.SelectedDates.Count == 0) return;

            var picked = sender.SelectedDates[0].Date;
            var minSqlDate = new DateTime(1753, 1, 1);

            if (picked >= minSqlDate)
            {
                ViewModel.SelectedDate = picked;
            }
        }

        private void DepartmentFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DepartmentFilterComboBox.SelectedItem is string selectedDept && _initialized)
            {
                ViewModel.SelectedDepartment = selectedDept;
            }
        }

        private void ViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender == DailyBtn)
            {
                DailyBtn.IsChecked = true;
                WeeklyBtn.IsChecked = false;
                ViewModel.IsWeeklyView = false;
            }
            else if (sender == WeeklyBtn)
            {
                WeeklyBtn.IsChecked = true;
                DailyBtn.IsChecked = false;
                ViewModel.IsWeeklyView = true;
            }
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

        private void ShowMessage(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(AdminShiftView));
        }
    }
}