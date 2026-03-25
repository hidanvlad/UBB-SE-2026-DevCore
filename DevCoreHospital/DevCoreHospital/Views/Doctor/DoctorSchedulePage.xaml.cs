using DevCoreHospital.Data;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views.Doctor
{
    public sealed partial class DoctorSchedulePage : Page
    {
        private readonly DoctorScheduleViewModel _vm;
        private readonly IDialogService _dialogService;

        public DoctorSchedulePage()
        {
            InitializeComponent();

            _dialogService = new DialogService();
            _vm = new DoctorScheduleViewModel(
                new CurrentUserService(),
                new DoctorAppointmentService(new SqlConnectionFactory()),
                _dialogService);

            DataContext = _vm;

            Loaded += DoctorSchedulePage_Loaded;
        }

        private async void DoctorSchedulePage_Loaded(object sender, RoutedEventArgs e)
        {
            _dialogService.SetXamlRoot(this.XamlRoot);
            await _vm.InitializeAsync();
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is AppointmentItemViewModel item)
                _vm.OpenDetails(item);
        }
    }
}