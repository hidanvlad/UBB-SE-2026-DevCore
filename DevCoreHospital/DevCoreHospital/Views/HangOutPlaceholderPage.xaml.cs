using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DevCoreHospital.ViewModels.Doctor; // <-- Make sure this is here!

namespace DevCoreHospital.Views
{
    public sealed partial class HangOutPlaceholderPage : Page
    {
        // 1. Declare the ViewModel property so the XAML can see it
        public HangoutViewModel ViewModel { get; }

        public HangOutPlaceholderPage()
        {
            this.InitializeComponent();

            // 2. Initialize the ViewModel
            ViewModel = new HangoutViewModel();
            this.DataContext = ViewModel;
        }

        // 3. Add the click handler for the Join button (if you used the XAML I provided earlier)
        private void Join_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int hangoutId)
            {
                ViewModel.JoinHangoutById(hangoutId);
            }
        }
    }
}