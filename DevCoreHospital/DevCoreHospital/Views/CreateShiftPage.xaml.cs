using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace DevCoreHospital.Views
{
    public sealed partial class CreateShiftPage : UserControl
    {
        public CreateShiftPage()
        {
            this.InitializeComponent();
            LoadStaffData();
        }

        private void LoadStaffData()
        {
            // Exemplu de date. Aici poți apela metodele colegilor tăi
            var staff = new List<string> { "Dr. Andrei Ionescu", "Dr. Elena Radu", "Farm. Mihai Pop" };
            EmployeeComboBox.ItemsSource = staff;
        }

        private void SaveShift_Click(object sender, RoutedEventArgs e)
        {
            // Validare de bază
            if (EmployeeComboBox.SelectedItem == null || ShiftDatePicker.Date == null)
            {
                ShowMessage("Eroare: Te rugăm să selectezi angajatul și data.", InfoBarSeverity.Error);
                return;
            }

            // Calculăm durata sau verificăm validitatea orelor
            var start = StartTimePicker.SelectedTime;
            var end = EndTimePicker.SelectedTime;

            if (end <= start)
            {
                ShowMessage("Atenție: Ora de final trebuie să fie după ora de început.", InfoBarSeverity.Warning);
                return;
            }

            // Aici vine codul pentru salvare în baza de date
            // shiftService.Save(employee, date, start, end);

            ShowMessage("Tura a fost salvată cu succes!", InfoBarSeverity.Success);
        }

        private void ShowMessage(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }
    }
}