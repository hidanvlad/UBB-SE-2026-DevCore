using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DevCoreHospital.Views.Doctor
{
    public sealed partial class AppointmentDetailsPage : Page
    {
        public string TypeLine { get; private set; } = "Type: -";
        public string LocationLine { get; private set; } = "Location: -";
        public string StatusLine { get; private set; } = "Status: -";
        public string TimeLine { get; private set; } = "Time: -";

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
                TypeLine = $"Type: {item.Type}";
                LocationLine = $"Location: {item.LocationSafe}";
                StatusLine = $"Status: {item.Status}";
                TimeLine = $"Time: {item.Date:yyyy-MM-dd} {item.TimeRangeText}";
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