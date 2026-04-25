using DevCoreHospital.Configuration;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DevCoreHospital.Views.Doctor
{
    public sealed partial class DoctorSchedulePage : Page
    {
        private readonly DoctorScheduleViewModel vm;
        private readonly DialogService dialogService;
        private bool initialized;

        public DoctorSchedulePage()
        {
            InitializeComponent();

            vm = App.Services.GetRequiredService<DoctorScheduleViewModel>();
            dialogService = App.Services.GetRequiredService<DialogService>();
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

            if (picked < AppSettings.SqlMinimumDate)
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
