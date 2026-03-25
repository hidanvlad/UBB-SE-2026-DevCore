using DevCoreHospital.Views;
using DevCoreHospital.Views.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital
{
    public sealed partial class MainWindow : Window
    {
        private bool _initialized;

        public MainWindow()
        {
            InitializeComponent();
            Activated += MainWindow_Activated;
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_initialized) return;
            _initialized = true;

            if (AppNavigationView.Content is Frame frame)
            {
                frame.Navigate(typeof(StartupPage));
            }
        }

        private void AppNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (sender.Content is not Frame frame) return;
            if (args.SelectedItemContainer is not NavigationViewItem item) return;
            if (item.Tag is not string tag) return;

            switch (tag)
            {
                case "MedicalEvaluation":
                    frame.Navigate(typeof(MedicalEvaluationView));
                    break;
                case "DoctorSchedule":
                    frame.Navigate(typeof(DoctorSchedulePage)); // or DoctorSchedulePage if that is your actual page
                    break;
            }
        }
    }
}