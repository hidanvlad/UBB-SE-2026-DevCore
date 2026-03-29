using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace DevCoreHospital.Views.Doctor
{
    public sealed partial class DoctorSchedulePage : Page
    {
        private readonly DoctorScheduleViewModel _vm;
        private readonly IDialogService _dialogService;
        private bool _initialized;

        public DoctorSchedulePage()
        {
            InitializeComponent();

            _dialogService = new DialogService();
            var dbManager = new DatabaseManager(AppSettings.ConnectionString);
            var appointmentRepository = new AppointmentRepository(dbManager);
            var fallbackDataSource = new FallbackDoctorAppointmentDataSource(
                appointmentRepository,
                new MockDoctorAppointmentDataSource());
            _vm = new DoctorScheduleViewModel(
                new CurrentUserService(),
                new DoctorAppointmentService(fallbackDataSource),
                _dialogService);

            DataContext = _vm;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _dialogService.SetXamlRoot(this.XamlRoot);

            if (_initialized) return;
            _initialized = true;

            await _vm.InitializeAsync();
        }

        private void DateCalendar_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (sender.SelectedDates == null || sender.SelectedDates.Count == 0)
                return;

            var picked = sender.SelectedDates[0].Date;
            var minSqlDate = new DateTime(1753, 1, 1);

            if (picked < minSqlDate)
                return;

            _vm.SelectedDate = picked;
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is AppointmentItemViewModel item)
                Frame?.Navigate(typeof(AppointmentDetailsPage), item);
        }
    }
}