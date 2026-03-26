using System.Collections.Generic;
using DevCoreHospital.Models;
using System.Linq;
using System;
using System.Diagnostics;

namespace DevCoreHospital.Data
{
    public class MedicalDataService
    {
        private static List<MedicalEvaluation> _mockTable = new List<MedicalEvaluation>();
        private static List<Shift> _shiftsMockTable = new List<Shift>();
        private static List<AdminNotification> _adminNotifications = new List<AdminNotification>();

        public MedicalDataService()
        {
            if (_mockTable.Count == 0)
            {
                _mockTable.Add(new MedicalEvaluation
                {
                    PatientId = "7759376",
                    Symptoms = "Historical Allergy to Penicillin recorded.",
                    EvaluationDate = DateTime.Now.AddYears(-1)
                });
            }

            if (_shiftsMockTable.Count == 0)
            {
                _shiftsMockTable.Add(new Shift { DoctorId = "DOC001", StartTime = DateTime.Now.AddHours(-9), EndTime = DateTime.Now.AddHours(-5), Status = ShiftStatus.COMPLETED });
                _shiftsMockTable.Add(new Shift { DoctorId = "DOC001", StartTime = DateTime.Now.AddHours(-2), Status = ShiftStatus.ACTIVE });
            }
        }
     

        public void SaveEvaluation(MedicalEvaluation record)
        {
            _mockTable.Add(record);
        }

        public List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId)
        {
            return _mockTable.Where(e => e.Evaluator != null && e.Evaluator.Id == doctorId).ToList();
        }

        public double GetDoctorFatigueHours(string doctorId)
        {
            return CalculateMockFatigue(doctorId);
        }

        public void CreateAdminFatigueAlert(string doctorId)
        {
            if (_adminNotifications.Any(n => n.DoctorId == doctorId && n.Timestamp > DateTime.Now.AddMinutes(-10))) return;
            _adminNotifications.Add(new AdminNotification { DoctorId = doctorId, Message = "Fatigue Alert: 12h exceeded.", Timestamp = DateTime.Now });
        }

        // task 30 METHODS
        public void UpdateAppointmentStatus(string patientId, string status)
        {
            Debug.WriteLine($">>>> SQL: Appointment for {patientId} set to {status}.");
        }

        public void UpdateDoctorAvailability(string doctorId)
        {
            Debug.WriteLine($">>>> SQL: Doctor {doctorId} availability updated.");
        }

        public List<MedicalEvaluation> GetPatientMedicalHistory(string patientId)
        {
            return _mockTable.Where(e => e.PatientId == patientId).OrderByDescending(e => e.EvaluationDate).ToList();
        }

        private double CalculateMockFatigue(string doctorId)
        {
            var now = DateTime.Now;
            var dayAgo = now.AddHours(-24);
            var active = _shiftsMockTable.FirstOrDefault(s => s.DoctorId == doctorId && s.Status == ShiftStatus.ACTIVE);
            double activeHours = active != null ? (now - active.StartTime).TotalHours : 0;
            double completedHours = _shiftsMockTable.Where(s => s.DoctorId == doctorId && s.Status == ShiftStatus.COMPLETED && s.EndTime >= dayAgo)
                .Sum(s => s.EndTime.HasValue ? (s.EndTime.Value - s.StartTime).TotalHours : 0);
            return activeHours + completedHours;
        }
    }
}