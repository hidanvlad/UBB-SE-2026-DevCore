using DevCoreHospital.Models;
using DevCoreHospital.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevCoreHospital.Repositories
{
    public class AppointmentRepository : IDoctorAppointmentDataSource
    {
        private readonly DatabaseManager _dbManager;

        public AppointmentRepository(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public async Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(int doctorUserId, DateTime fromDate, int skip, int take)
        {
            var toDate = fromDate.Date.AddDays(8);
            // Așteptăm rezultatul de la baza de date
            return await _dbManager.GetUpcomingAppointmentsAsync(doctorUserId, fromDate.Date, toDate, skip, take);
        }

        public async Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
        {
            return await _dbManager.GetAllDoctorsAsync();
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            return await _dbManager.GetAppointmentDetailsAsync(appointmentId);
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId)
        {
            return await _dbManager.GetAppointmentsForAdminAsync(doctorId);
        }

        public async Task AddAppointmentAsync(Appointment appt)
        {
            // Extragem ID-ul pacientului din text (ex: "PAT-500" devine 500)
            string rawPatientInput = appt.PatientName?.Replace("PAT-", "").Trim() ?? "0";
            int.TryParse(rawPatientInput, out int patientId);

            // Combinăm data cu ora pentru a crea un DATETIME complet pentru SQL
            DateTime startTimeDb = appt.Date.Date.Add(appt.StartTime);
            DateTime endTimeDb = appt.Date.Date.Add(appt.EndTime);

            await _dbManager.AddAppointmentAsync(patientId, appt.DoctorId, startTimeDb, endTimeDb);
        }

        public async Task UpdateAppointmentStatusAsync(int id, string status)
        {
            await _dbManager.UpdateAppointmentStatusAsync(id, status);
        }

        public async Task<int> GetActiveAppointmentsCountForDoctorAsync(int doctorId)
        {
            return await _dbManager.GetActiveAppointmentsCountAsync(doctorId);
        }

        public async Task UpdateDoctorStatusAsync(int doctorId, string status)
        {
            await _dbManager.UpdateDoctorStatusAsync(doctorId, status);
        }
    }
}