using System;
using DevCoreHospital.Configuration;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DevCoreHospital.Views.Doctor
{
    public sealed partial class DoctorSchedulePage : Page
    {
        private readonly DoctorScheduleViewModel vm;
        private readonly IDialogService dialogService;
        private bool initialized;

        public DoctorSchedulePage()
        {
            InitializeComponent();

            dialogService = new DialogService();
            var staffRepo = new StaffRepository(AppSettings.ConnectionString);
            var appointmentRepository = new AppointmentRepository(AppSettings.ConnectionString);
            var shiftRepository = new ShiftRepository(AppSettings.ConnectionString, staffRepo);
            vm = new DoctorScheduleViewModel(
                new CurrentUserService(),
                new DoctorAppointmentService(appointmentRepository),
                shiftRepository,
                dialogService);

            DataContext = vm;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            dialogService.SetXamlRoot(this.XamlRoot);

            if (initialized)
            {
                return;
            }

            initialized = true;

            await vm.InitializeAsync();
        }

        private void DateCalendar_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (sender.SelectedDates == null || sender.SelectedDates.Count == 0)
            {
                return;
            }

            var picked = sender.SelectedDates[0].Date;
            var minSqlDate = new DateTime(1753, 1, 1);

            if (picked < minSqlDate)
            {
                return;
            }

            vm.SelectedDate = picked;
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is AppointmentItemViewModel item)
            {
                Frame?.Navigate(typeof(AppointmentDetailsPage), item.ToAppointment());
            }
        }
    }
}
