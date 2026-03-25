using DevCoreHospital.Data;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DevCoreHospital.Views.Doctor
{
    public sealed partial class DoctorSchedulePage : Page
    {
        public DoctorScheduleViewModel ViewModel { get; }

        public DoctorSchedulePage()
        {
            InitializeComponent();

            var sqlFactory = new SqlConnectionFactory();
            var appointmentService = new DoctorAppointmentService(sqlFactory);
            ICurrentUserService currentUserService = new CurrentUserService();

            ViewModel = new DoctorScheduleViewModel(currentUserService, appointmentService);
            DataContext = ViewModel;

            Loaded += DoctorSchedulePage_Loaded;
        }

        private void DoctorSchedulePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel.RefreshCommand?.CanExecute(null) == true)
            {
                ViewModel.RefreshCommand.Execute(null);
            }
        }

        private async void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Appointment Details",
                Content = "You can bind selected appointment full details here.",
                CloseButtonText = "Close",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}