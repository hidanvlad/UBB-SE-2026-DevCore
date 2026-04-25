using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using DevCoreHospital.ViewModels.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;

namespace DevCoreHospital.Views.Admin
{
    public sealed partial class AdminSchedulePage : Page
    {
        public AdminShiftViewModel ViewModel { get; }
        private bool initialized;

        public AdminSchedulePage()
        {
            this.InitializeComponent();

            ViewModel = App.Services.GetRequiredService<AdminShiftViewModel>();
            DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (initialized)
            {
                return;
            }

            initialized = true;

            ViewModel.LoadAndFilterShifts();
            DateCalendar.SelectedDates.Add(System.DateTime.Today);
        }

        private void DateCalendar_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs eventArgs)
        {
            if (sender.SelectedDates == null || sender.SelectedDates.Count == 0)
            {
                return;
            }

            var picked = sender.SelectedDates[0].Date;

            if (picked >= AppSettings.SqlMinimumDate)
            {
                ViewModel.SelectedDate = picked;
            }
        }

        private void DepartmentFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DepartmentFilterComboBox.SelectedItem is string selectedDept && initialized)
            {
                ViewModel.SelectedDepartment = selectedDept;
            }
        }

        private void ViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, DailyBtn))
            {
                DailyBtn.IsChecked = true;
                WeeklyBtn.IsChecked = false;
                ViewModel.IsWeeklyView = false;
            }
            else if (ReferenceEquals(sender, WeeklyBtn))
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
