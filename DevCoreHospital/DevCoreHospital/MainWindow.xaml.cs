using DevCoreHospital.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            RootFrame.Navigate(typeof(RoleSelectionPage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs eventArgs)
        {
            if (eventArgs.SelectedItemContainer is not NavigationViewItem selected)
            {
                return;
            }

            var tag = selected.Tag?.ToString();
            if (tag == "role-selection")
            {
                RootFrame.Navigate(typeof(RoleSelectionPage));
            }
            else if (tag == "dashboard")
            {
                RootFrame.Navigate(typeof(RoleDashboardPage));
            }
        }
    }
}
