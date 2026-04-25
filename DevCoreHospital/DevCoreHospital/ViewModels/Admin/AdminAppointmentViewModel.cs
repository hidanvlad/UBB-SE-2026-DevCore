using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Base;

namespace DevCoreHospital.ViewModels
{
    public class AdminAppointmentsViewModel : INotifyPropertyChanged
    {
        private readonly IDoctorAppointmentService appointmentService;

        public ObservableCollection<DoctorOption> Doctors { get; } = new ObservableCollection<DoctorOption>();
        public ObservableCollection<Appointment> AppointmentsList { get; } = new ObservableCollection<Appointment>();

        public AdminAppointmentsViewModel(IDoctorAppointmentService appointmentService)
        {
            this.appointmentService = appointmentService;
        }

        public async Task LoadDoctorsAsync()
        {
            var doctors = await appointmentService.GetAllDoctorsAsync();
            Doctors.ReplaceWith(doctors.Select(DoctorOption.From));
        }

        public async Task LoadAppointmentsForDoctorAsync(int doctorId)
        {
            var appointments = await appointmentService.GetAppointmentsForAdminAsync(doctorId);
            AppointmentsList.ReplaceWith(appointments);
        }

        public async Task BookAppointmentAsync(string patientId, int doctorId, DateTime date, TimeSpan time)
        {
            await appointmentService.CreateAppointmentAsync(patientId, doctorId, date, time);
        }

        public async Task FinishAppointmentAsync(Appointment appointment)
        {
            await appointmentService.FinishAppointmentAsync(appointment);
        }

        public async Task CancelAppointmentAsync(Appointment appointment)
        {
            await appointmentService.CancelAppointmentAsync(appointment);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public sealed class DoctorOption
        {
            public int DoctorId { get; set; }
            public string DoctorName { get; set; } = string.Empty;

            public static DoctorOption From((int DoctorId, string DoctorName) doctor) =>
                new DoctorOption
                {
                    DoctorId = doctor.DoctorId,
                    DoctorName = string.IsNullOrWhiteSpace(doctor.DoctorName) ? $"Doctor #{doctor.DoctorId}" : doctor.DoctorName,
                };
        }
    }
}