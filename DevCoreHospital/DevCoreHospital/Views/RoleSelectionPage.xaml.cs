using DevCoreHospital.Models;
using DevCoreHospital.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevCoreHospital.Views
{
    public sealed partial class RoleSelectionPage : Page
    {
        private readonly ICurrentUserService currentUser;

        public RoleSelectionPage()
        {
            InitializeComponent();

            currentUser = App.Services.GetRequiredService<ICurrentUserService>();
        }

        private void Admin_Click(object sender, RoutedEventArgs e)
        {
            currentUser.RoleType = UserRole.Admin;
            Frame.Navigate(typeof(RoleDashboardPage));
        }

        private void Doctor_Click(object sender, RoutedEventArgs e)
        {
            currentUser.RoleType = UserRole.Doctor;
            Frame.Navigate(typeof(RoleDashboardPage));
        }

        private void Pharmacist_Click(object sender, RoutedEventArgs e)
        {
            currentUser.RoleType = UserRole.Pharmacist;
            Frame.Navigate(typeof(RoleDashboardPage));
        }
    }
}
