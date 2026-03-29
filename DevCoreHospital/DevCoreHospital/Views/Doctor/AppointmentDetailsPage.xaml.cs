using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace DevCoreHospital.Views
{
    public sealed partial class AppointmentDetailsPage : Page
    {
        private Appointment _currentAppointment;
        private DoctorAppointmentService _service;

        public AppointmentDetailsPage()
        {
            this.InitializeComponent();

            var dbManager = new DatabaseManager(AppSettings.ConnectionString);
            var repo = new AppointmentRepository(dbManager);
            var fallback = new FallbackDoctorAppointmentDataSource(repo, new MockDoctorAppointmentDataSource());
            _service = new DoctorAppointmentService(fallback);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Appointment appt)
            {
                _currentAppointment = appt;
                PopulateData();
            }
        }

        private void PopulateData()
        {
            PatientNameText.Text = _currentAppointment.PatientName;
            DoctorNameText.Text = _currentAppointment.DoctorName;
            DateText.Text = _currentAppointment.Date.ToString("yyyy-MM-dd");
            TimeText.Text = $"{_currentAppointment.StartTime:hh\\:mm} - {_currentAppointment.EndTime:hh\\:mm}";
            StatusText.Text = _currentAppointment.Status;
        }

        private async void FinishBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAppointment.Status == "Finished")
            {
                ShowMessage("This appointment is already finished.", InfoBarSeverity.Warning);
                return;
            }

            try
            {
                await _service.FinishAppointmentAsync(_currentAppointment);

                _currentAppointment.Status = "Finished";
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