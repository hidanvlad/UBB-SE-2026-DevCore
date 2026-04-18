using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using DevCoreHospital.ViewModels.Doctor;

namespace DevCoreHospital.Views
{
    public sealed partial class HangOutPlaceholderPage : Page
    {
        public HangoutViewModel ViewModel { get; }

        public HangOutPlaceholderPage()
        {
            this.InitializeComponent();

            ViewModel = new HangoutViewModel();
            this.DataContext = ViewModel;
        }

        private void Join_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int hangoutId)
            {
                ViewModel.JoinHangoutById(hangoutId);
            }
        }
    }
}