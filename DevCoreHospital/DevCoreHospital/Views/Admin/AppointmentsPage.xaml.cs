using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using DevCoreHospital.Models;
using DevCoreHospital.ViewModels;
using DevCoreHospital.Services;
using DevCoreHospital.Repositories;
using DevCoreHospital.Configuration;
using DevCoreHospital.Data;

namespace DevCoreHospital.Views
{
    public sealed partial class AppointmentsPage : Page
    {
        public AdminAppointmentsViewModel ViewModel { get; }

        public AppointmentsPage()
        {
            this.InitializeComponent();

            var dbManager = new DatabaseManager(AppSettings.ConnectionString); // Create database manager
            var appointmentRepository = new AppointmentRepository(dbManager); // Create repository using database manager
            var fallbackDataSource = new FallbackDoctorAppointmentDataSource(
                appointmentRepository,
                new MockDoctorAppointmentDataSource());
            var service = new DoctorAppointmentService(fallbackDataSource); // Pass repository with mock fallback to service

            ViewModel = new AdminAppointmentsViewModel(service);
            DataContext = ViewModel;

            // Încărcăm doctorii la deschiderea paginii
            Loaded += AppointmentsPage_Loaded;
        }

        private async void AppointmentsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadDoctorsAsync();

            if (ViewModel.Doctors.Count > 0)
            {
                DoctorComboBox.SelectedIndex = 0;
                FilterDoctorComboBox.SelectedIndex = 0;
            }
        }

        private async void BookAppointment_Click(object sender, RoutedEventArgs e)
        {
            string patientId = PatientIdTextBox.Text;

            // Verificăm dacă a selectat un doctor din listă
            if (DoctorComboBox.SelectedValue is not int selectedDoctorId ||
                string.IsNullOrWhiteSpace(patientId) ||
                AppointmentDatePicker.Date == null ||
                AppointmentTimePicker.SelectedTime == null)
            {
                ShowMessage("Please fill in all fields (Doctor, Patient ID, Date, Time).", InfoBarSeverity.Error);
                return;
            }

            try
            {
                DateTime date = AppointmentDatePicker.Date.Value.DateTime;
                TimeSpan time = AppointmentTimePicker.SelectedTime.Value;

                await ViewModel.BookAppointmentAsync(patientId, selectedDoctorId, date, time);

                ShowMessage($"Appointment booked successfully for {patientId}!", InfoBarSeverity.Success);

                // Resetăm formularul
                PatientIdTextBox.Text = string.Empty;
                DoctorComboBox.SelectedIndex = -1;

                // Reîncărcăm calendarul dacă doctorul selectat e același cu cel din filtru
                if (FilterDoctorComboBox.SelectedValue is int filterDocId && filterDocId == selectedDoctorId)
                {
                    await ViewModel.LoadAppointmentsForDoctorAsync(filterDocId);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error booking appointment: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private async void FilterDoctorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilterDoctorComboBox.SelectedValue is int doctorId)
            {
                await ViewModel.LoadAppointmentsForDoctorAsync(doctorId);
            }
        }

        private async void FinishAppointment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Appointment appt)
            {
                await ViewModel.FinishAppointmentAsync(appt);
                ShowMessage("Appointment marked as Finished.", InfoBarSeverity.Success);

                // Reîmprospătăm lista
                if (FilterDoctorComboBox.SelectedValue is int doctorId)
                    await ViewModel.LoadAppointmentsForDoctorAsync(doctorId);
            }
        }

        private async void CancelAppointment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Appointment appt)
            {
                if (appt.Status == "Finished")
                {
                    ShowMessage("Cannot cancel an appointment that is already Finished!", InfoBarSeverity.Error);
                    return;
                }

                await ViewModel.CancelAppointmentAsync(appt);
                ShowMessage("Appointment successfully canceled.", InfoBarSeverity.Informational);

                if (FilterDoctorComboBox.SelectedValue is int doctorId)
                    await ViewModel.LoadAppointmentsForDoctorAsync(doctorId);
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