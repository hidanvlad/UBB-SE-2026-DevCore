using DevCoreHospital.Configuration;
using DevCoreHospital.Data;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using DevCoreHospital.Views.Doctor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DevCoreHospital.Views
{
    public sealed partial class AppointmentsPage : Page
    {
        public AdminAppointmentsViewModel ViewModel { get; }

        public AppointmentsPage()
        {
            this.InitializeComponent();

            var dbManager = new DatabaseManager(AppSettings.ConnectionString);
            var appointmentRepository = new AppointmentRepository(dbManager);
            var service = new DoctorAppointmentService(appointmentRepository);

            ViewModel = new AdminAppointmentsViewModel(service);
            DataContext = ViewModel;

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

                PatientIdTextBox.Text = string.Empty;
                DoctorComboBox.SelectedIndex = -1;

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

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Appointment appt)
            {
                this.Frame.Navigate(typeof(AppointmentDetailsPage), appt);
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