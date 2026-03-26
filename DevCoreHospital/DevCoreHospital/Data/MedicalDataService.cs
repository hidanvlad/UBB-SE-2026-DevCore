using System.Collections.Generic;
using DevCoreHospital.Models;
using System.Linq;
using System;

namespace DevCoreHospital.Data
{
    public class MedicalDataService
    {
        private static List<MedicalEvaluation> _mockTable = new List<MedicalEvaluation>();
        private static List<Shift> _shiftsMockTable = new List<Shift>();

        public MedicalDataService()
        {
            if (_shiftsMockTable.Count == 0)
            {
                _shiftsMockTable.Add(new Shift(1, new Doctor(1, "John", "Doe", "0700-000 000", true, "Cardiology", "12345", DoctorStatus.AVAILABLE), "Cardiology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.ACTIVE));
                _shiftsMockTable.Add(new Shift(2, new Doctor(2, "Jane", "Smith", "0700-000 001", false, "Neurology", "54321", DoctorStatus.IN_EXAMINATION), "Neurology", DateTime.Now, DateTime.Now.AddHours(8), ShiftStatus.SCHEDULED));
            }
        }

  
        public void UpdateEvaluationNotes(int evaluationId, string newNotes)
        {

            var record = _mockTable.FirstOrDefault(e => e.EvaluationID == evaluationId);
            if (record != null)
            {
                record.Notes = newNotes;
            }
        }

        public void SaveEvaluation(MedicalEvaluation record)
        {
     
            _mockTable.Add(record);
        }

        public List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId)
        {
            return _mockTable.Where(e => e.Evaluator != null && e.Evaluator.StaffID.ToString() == doctorId).ToList();
        }

        public List<MedicalEvaluation> GetPatientMedicalHistory(string patientId)
        {
            return _mockTable
                .Where(e => string.Equals(e.PatientId, patientId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.EvaluationDate)
                .ToList();
        }

        public void UpdateAppointmentStatus(string patientId, string status) { }
        public void UpdateDoctorAvailability(string doctorId) { }
        public void CreateAdminFatigueAlert(string doctorId) { }

        public double GetDoctorFatigueHours(string doctorId)
        {
            return CalculateMockFatigue(doctorId);
        }

        private double CalculateMockFatigue(string doctorId)
        {
            var now = DateTime.Now;
            var dayAgo = now.AddHours(-24);
            var active = _shiftsMockTable.FirstOrDefault(s => s.AppointedStaff != null && s.AppointedStaff.StaffID.ToString() == doctorId && s.Status == ShiftStatus.ACTIVE);
            double activeHours = active != null ? (now - active.StartTime).TotalHours : 0;

            double completedHours = _shiftsMockTable
                .Where(s => s.AppointedStaff != null && s.AppointedStaff.StaffID.ToString() == doctorId && s.Status == ShiftStatus.COMPLETED && s.EndTime >= dayAgo)
                .Sum(s => (s.EndTime - s.StartTime).TotalHours);

            return activeHours + completedHours;
        }

        public void DeleteEvaluation(int evaluationId)
        {
            // TASK 12: Hand-written SQL DELETE for local SQL Server
            // The query must be simple and free of business logic.
            string sql = "DELETE FROM MedicalEvaluations WHERE EvaluationID = @Id";

            // In-memory implementation for now:
            var record = _mockTable.FirstOrDefault(e => e.EvaluationID == evaluationId);
            if (record != null)
            {
                _mockTable.Remove(record);
            }
        }
    }
}