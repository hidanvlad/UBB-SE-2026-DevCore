using System;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;

namespace DevCoreHospital.Views
{
    public sealed partial class AppointmentDetailsPage : Page
    {
        private Appointment? currentAppointment;
        private readonly IDoctorAppointmentService service;

        public AppointmentDetailsPage()
        {
            this.InitializeComponent();

            service = App.Services.GetRequiredService<IDoctorAppointmentService>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs eventArgs)
        {
            base.OnNavigatedTo(eventArgs);

            if (eventArgs.Parameter is Appointment appt)
            {
                currentAppointment = appt;
                PopulateData();
            }
        }

        private void PopulateData()
        {
            if (currentAppointment == null)
            {
                return;
            }

            PatientNameText.Text = currentAppointment.PatientName;
            DoctorNameText.Text = currentAppointment.DoctorName;
            DateText.Text = currentAppointment.Date.ToString("yyyy-MM-dd");
            TimeText.Text = $"{currentAppointment.StartTime:hh\\:mm} - {currentAppointment.EndTime:hh\\:mm}";
            StatusText.Text = currentAppointment.Status;
        }

        private async void FinishBtn_Click(object sender, RoutedEventArgs eventArgs)
        {
            if (currentAppointment == null)
            {
                return;
            }

            try
            {
                await service.FinishAppointmentAsync(currentAppointment!);

                currentAppointment!.Status = "Finished";
                PopulateData();
                ShowMessage("Appointment finished successfully! Doctor status updated.", InfoBarSeverity.Success);
            }
            catch (Exception exception)
            {
                ShowMessage($"Error: {exception.Message}", InfoBarSeverity.Error);
            }
        }

        private void GoBack_Click(object sender, RoutedEventArgs eventArgs)
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
