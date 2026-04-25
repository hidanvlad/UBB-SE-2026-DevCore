using DevCoreHospital.Configuration;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using DevCoreHospital.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DevCoreHospital.Views.Doctor
{
    public sealed partial class DoctorSchedulePage : Page
    {
        private readonly DoctorScheduleViewModel viewModel;
        private readonly DialogPresenter dialogPresenter;
        private bool initialized;

        public DoctorSchedulePage()
        {
            InitializeComponent();

            viewModel = App.Services.GetRequiredService<DoctorScheduleViewModel>();
            dialogPresenter = App.Services.GetRequiredService<DialogPresenter>();
            DataContext = viewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            dialogPresenter.SetXamlRoot(this.XamlRoot);

            if (initialized)
            {
                return;
            }

            initialized = true;

            await viewModel.InitializeAsync();
        }

        private void DateCalendar_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs eventArgs)
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

            viewModel.SelectedDate = picked;
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
