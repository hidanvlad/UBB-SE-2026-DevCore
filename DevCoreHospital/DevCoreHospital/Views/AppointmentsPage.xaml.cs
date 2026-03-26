using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using DevCoreHospital.Models;

namespace DevCoreHospital.Views
{
    public sealed partial class AppointmentsPage : Page
    {
        public AppointmentsPage()
        {
            this.InitializeComponent();
            LoadDoctors();
        }

        private void LoadDoctors()
        {
            // TODO: Încarcă lista de doctori din baza de date
            // ex: var doctors = await _service.GetAllDoctorsAsync();
            // DoctorComboBox.ItemsSource = doctors;
            // FilterDoctorComboBox.ItemsSource = doctors;
        }

        // ==========================================
        // CERINȚA: BOOK APPOINTMENT
        // ==========================================
        private void BookAppointment_Click(object sender, RoutedEventArgs e)
        {
            string patientId = PatientIdTextBox.Text;

            if (DoctorComboBox.SelectedItem == null || string.IsNullOrWhiteSpace(patientId) ||
                AppointmentDatePicker.Date == null || AppointmentTimePicker.SelectedTime == null)
            {
                ShowMessage("Please fill in all fields (Doctor, Patient ID, Date, Time).", InfoBarSeverity.Error);
                return;
            }

            // Aici combini data și ora
            DateTime appointmentDate = AppointmentDatePicker.Date.Value.DateTime.Add(AppointmentTimePicker.SelectedTime.Value);

            // TODO: Salvarea în baza de date
            // Regulă tabel: Appointments.patient_id folosește external_ref_id (patientId)
            // ex: _appointmentService.BookAppointment(doctorId, patientId, appointmentDate);

            ShowMessage($"Appointment booked successfully for Patient {patientId}!", InfoBarSeverity.Success);

            // Reset form
            PatientIdTextBox.Text = string.Empty;
            DoctorComboBox.SelectedIndex = -1;
        }

        // ==========================================
        // CERINȚA: FINISH APPOINTMENT
        // ==========================================
        private void FinishAppointment_Click(object sender, RoutedEventArgs e)
        {
            // Presupunem că obiectul legat este de tip 'Appointment' (va trebui să îți creezi clasa asta)
            if (sender is Button btn && btn.Tag is Appointment appointmentToFinish)
            {
                // TODO: Update în baza de date
                // 1. Setăm statusul programării la "Finished"
                // 2. Cerința: "Automatically trigger a check: if doctor has no other concurrent appointments, 
                //    their doctorStatus must revert from IN_EXAMINATION to AVAILABLE."

                /* LOGICĂ BACKEND RECOMANDATĂ PENTRU SERVICIUL TĂU:
                   
                   _appointmentRepo.UpdateStatus(appointmentToFinish.Id, "Finished");
                   
                   bool hasConcurrent = _appointmentRepo.CheckActiveAppointmentsForDoctor(appointmentToFinish.DoctorId);
                   if (!hasConcurrent) {
                       _doctorRepo.UpdateStatus(appointmentToFinish.DoctorId, "AVAILABLE");
                   }
                */

                ShowMessage("Appointment marked as Finished. Doctor status updated if necessary.", InfoBarSeverity.Success);
                RefreshAppointmentsList(); // Reîncărcăm lista pe ecran
            }
        }

        // ==========================================
        // CERINȚA: CANCEL APPOINTMENT
        // ==========================================
        private void CancelAppointment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Appointment appointmentToCancel)
            {
                // Cerința: "System must validate that the status is not already 'Finished'"
                if (appointmentToCancel.Status == "Finished")
                {
                    ShowMessage("Cannot cancel an appointment that is already Finished!", InfoBarSeverity.Error);
                    return;
                }

                // TODO: Update în baza de date (Appointments.status -> Canceled)
                // ex: _appointmentService.CancelAppointment(appointmentToCancel.Id);

                ShowMessage("Appointment successfully canceled.", InfoBarSeverity.Informational);
                RefreshAppointmentsList();
            }
        }

        // ==========================================
        // CERINȚA: VIEW CALENDAR / APPOINTMENTS
        // ==========================================
        private void FilterDoctorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshAppointmentsList();
        }

        private void RefreshAppointmentsList()
        {
            // TODO: Adu programările din DB pentru doctorul selectat în FilterDoctorComboBox
            // var selectedDoctor = FilterDoctorComboBox.SelectedItem;
            // var appointments = await _appointmentService.GetUpcomingAppointmentsForDoctor(selectedDoctor.Id);
            // AppointmentsListView.ItemsSource = appointments;
        }

        private void ShowMessage(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }
    }
}