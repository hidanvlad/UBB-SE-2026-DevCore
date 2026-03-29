using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DevCoreHospital.Views.Doctor
{
    public sealed partial class AppointmentDetailsPage : Page
    {
        public string AppointmentIdLine { get; private set; } = "Appointment ID: -";
        public string PatientIdLine { get; private set; } = "Patient ID: -";
        public string DoctorIdLine { get; private set; } = "Doctor ID: -";
        public string TimeLine { get; private set; } = "Date & Time: -";
        public string StatusLine { get; private set; } = "Status: -";

        public AppointmentDetailsPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is AppointmentItemViewModel item)
            {
                AppointmentIdLine = $"Appointment ID: {item.Id}";
                PatientIdLine = $"Patient ID: {item.PatientId}";
                DoctorIdLine = $"Doctor ID: {item.DoctorId}";
                TimeLine = $"Date & Time: {item.DateTime:yyyy-MM-dd HH:mm}";
                StatusLine = $"Status: {item.Status}";
            }

            DataContext = null;
            DataContext = this;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame?.CanGoBack == true)
                Frame.GoBack();
        }
    }
}