using DevCoreHospital.Models;
using DevCoreHospital.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views
{
    public sealed partial class RoleSelectionPage : Page
    {
        private readonly ICurrentUserService _currentUser = new CurrentUserService();

        public RoleSelectionPage()
        {
            InitializeComponent();
        }

        private void Admin_Click(object sender, RoutedEventArgs e)
        {
            _currentUser.RoleType = UserRole.Admin;
            Frame.Navigate(typeof(RoleDashboardPage));
        }

        private void Doctor_Click(object sender, RoutedEventArgs e)
        {
            _currentUser.RoleType = UserRole.Doctor;
            Frame.Navigate(typeof(RoleDashboardPage));
        }

        private void Pharmacist_Click(object sender, RoutedEventArgs e)
        {
            _currentUser.RoleType = UserRole.Pharmacist;
            Frame.Navigate(typeof(RoleDashboardPage));
        }
    }
}