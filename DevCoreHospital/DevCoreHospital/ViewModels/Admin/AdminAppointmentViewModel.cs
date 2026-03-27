using DevCoreHospital.Models;
using DevCoreHospital.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DevCoreHospital.ViewModels
{
    public class AdminAppointmentsViewModel : INotifyPropertyChanged
    {
        private readonly IDoctorAppointmentService _appointmentService;

        // Listele observabile care vor actualiza automat interfața (UI-ul) când adăugăm/ștergem elemente
        public ObservableCollection<DoctorOption> Doctors { get; } = new();
        public ObservableCollection<Appointment> AppointmentsList { get; } = new ObservableCollection<Appointment>();

        public AdminAppointmentsViewModel(IDoctorAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
        }

        // --- METODE DE ÎNCĂRCARE DATE ---

        public async Task LoadDoctorsAsync()
        {
            var doctors = await _appointmentService.GetAllDoctorsAsync();
            Doctors.Clear();
            foreach (var doc in doctors)
            {
                Doctors.Add(new DoctorOption
                {
                    DoctorId = doc.DoctorId,
                    DoctorName = string.IsNullOrWhiteSpace(doc.DoctorName) ? $"Doctor #{doc.DoctorId}" : doc.DoctorName
                });
            }
        }

        public async Task LoadAppointmentsForDoctorAsync(int doctorId)
        {
            var apps = await _appointmentService.GetAppointmentsForAdminAsync(doctorId);
            AppointmentsList.Clear();
            foreach (var app in apps)
            {
                AppointmentsList.Add(app);
            }
        }

        // --- METODE DE ACȚIUNE (CREATE, UPDATE, CANCEL) ---

        public async Task BookAppointmentAsync(string patientId, int doctorId, DateTime date, TimeSpan time)
        {
            var newAppointment = new Appointment
            {
                PatientName = patientId,
                DoctorId = doctorId,
                Date = date.Date,
                StartTime = time,
                // Presupunem că o consultație standard durează 30 de minute
                EndTime = time.Add(TimeSpan.FromMinutes(30)),
                Status = "Scheduled"
            };

            await _appointmentService.BookAppointmentAsync(newAppointment);
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            await _appointmentService.FinishAppointmentAsync(appointment);
        }

        public async Task CancelAppointmentAsync(Appointment appointment)
        {
            await _appointmentService.CancelAppointmentAsync(appointment);
        }

        // --- LOGICA PENTRU INotifyPropertyChanged ---
        // (Asta îi spune interfeței când o variabilă s-a modificat)
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public sealed class DoctorOption
        {
            public int DoctorId { get; set; }
            public string DoctorName { get; set; } = string.Empty;
        }
    }
}