using DevCoreHospital.Data;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using DevCoreHospital.Services;


namespace DevCoreHospital.Views.Doctor;

public sealed partial class DoctorSchedulePage : Page
{
    public DoctorScheduleViewModel ViewModel { get; }

    public DoctorSchedulePage()
    {
        this.InitializeComponent();

        var connectionString = "Server=localhost;Database=DevCoreHospital;Trusted_Connection=True;TrustServerCertificate=True;";
        var sqlFactory = new SqlConnectionFactory(connectionString);
        IDoctorAppointmentService appointmentService = new Services.DoctorAppointmentService(sqlFactory);
        ICurrentUserService currentUser = new MockCurrentUserService();

        ViewModel = new DoctorScheduleViewModel(currentUser, appointmentService);
        ViewModel.OpenAppointmentDetailsRequested = OpenAppointmentDetails;
        DataContext = ViewModel;

        _ = ViewModel.InitializeAsync();
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is int id)
        {
            var selected = ViewModel.Appointments.FirstOrDefault(x => x.Id == id);
            ViewModel.OpenDetails(selected);
        }
    }

    private async void OpenAppointmentDetails(int appointmentId)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = $"Appointment #{appointmentId}",
            Content = "Open your details panel/modal here.",
            CloseButtonText = "Close"
        };

        await dialog.ShowAsync(); // WinUI IAsyncOperation works in async method
    }
}