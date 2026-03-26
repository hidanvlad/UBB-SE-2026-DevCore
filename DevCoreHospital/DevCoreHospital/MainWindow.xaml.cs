using DevCoreHospital.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DevCoreHospital
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            RootFrame.Navigated += RootFrame_Navigated;
            NavigateTo(typeof(RoleSelectionPage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem item)
                return;

            switch (item.Tag?.ToString())
            {
                case "role-selection":
                    NavigateTo(typeof(RoleSelectionPage));
                    break;
                case "dashboard":
                    NavigateTo(typeof(RoleDashboardPage));
                    break;
            }
        }

        private void RootFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(RoleSelectionPage))
            {
                NavView.SelectedItem = FindItemByTag("role-selection");
            }
            else if (e.SourcePageType == typeof(RoleDashboardPage))
            {
                NavView.SelectedItem = FindItemByTag("dashboard");
            }
        }

        private void NavigateTo(Type pageType)
        {
            if (RootFrame.CurrentSourcePageType == pageType)
                return;

            RootFrame.Navigate(pageType);
        }

        private NavigationViewItem? FindItemByTag(string tag)
        {
            foreach (var mi in NavView.MenuItems)
            {
                if (mi is NavigationViewItem nvi && string.Equals(nvi.Tag?.ToString(), tag, StringComparison.Ordinal))
                    return nvi;
            }

            return null;
        }
    }
}