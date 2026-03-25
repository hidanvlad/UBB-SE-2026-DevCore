using DevCoreHospital.Views;
using Microsoft.UI.Xaml;

namespace DevCoreHospital
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            RootFrame.Navigate(typeof(RoleSelectionPage));
        }
    }
}