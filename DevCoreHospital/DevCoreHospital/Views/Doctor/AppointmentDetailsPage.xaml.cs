using System;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;

namespace DevCoreHospital.Views
{
    public sealed partial class AppointmentDetailsPage : Page
    {
        private Appointment currentAppointment;
        private DoctorAppointmentService service;

        public AppointmentDetailsPage()
        {
            this.InitializeComponent();

            var repo = new AppointmentRepository(AppSettings.ConnectionString);
            service = new DoctorAppointmentService(repo);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Appointment appt)
            {
                currentAppointment = appt;
                PopulateData();
            }
        }

        private void PopulateData()
        {
            PatientNameText.Text = currentAppointment.PatientName;
            DoctorNameText.Text = currentAppointment.DoctorName;
            DateText.Text = currentAppointment.Date.ToString("yyyy-MM-dd");
            TimeText.Text = $"{currentAppointment.StartTime:hh\\:mm} - {currentAppointment.EndTime:hh\\:mm}";
            StatusText.Text = currentAppointment.Status;
        }

        private async void FinishBtn_Click(object sender, RoutedEventArgs e)
        {
            if (currentAppointment.Status == "Finished")
            {
                ShowMessage("This appointment is already finished.", InfoBarSeverity.Warning);
                return;
            }

            try
            {
                await service.FinishAppointmentAsync(currentAppointment);

                currentAppointment.Status = "Finished";
                PopulateData();
                ShowMessage("Appointment finished successfully! Doctor status updated.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowMessage($"Error: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void GoBack_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        private void ShowMessage(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }
    }
}
