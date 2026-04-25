using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Configuration;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public sealed class MedicalEvaluationService : IMedicalEvaluationService
    {
        private const double FatigueThresholdHours = 12.0;
        private const double FatigueLookbackHours = 24.0;
        private const string ConfirmedAppointmentStatus = "Confirmed";
        private const string AllergyKeyword = "Allergy";
        private const string AdverseKeyword = "Adverse";
        private const string RiskMarker = "[RISK]";

        private readonly IEvaluationsRepository evaluationsRepository;
        private readonly IHighRiskMedicineRepository highRiskMedicineRepository;
        private readonly IAppointmentRepository appointmentRepository;
        private readonly IStaffRepository staffRepository;
        private readonly IShiftRepository shiftRepository;

        public MedicalEvaluationService(
            IEvaluationsRepository evaluationsRepository,
            IHighRiskMedicineRepository highRiskMedicineRepository,
            IAppointmentRepository appointmentRepository,
            IStaffRepository staffRepository,
            IShiftRepository shiftRepository)
        {
            this.evaluationsRepository = evaluationsRepository;
            this.highRiskMedicineRepository = highRiskMedicineRepository;
            this.appointmentRepository = appointmentRepository;
            this.staffRepository = staffRepository;
            this.shiftRepository = shiftRepository;
        }

        public List<Doctor> GetAllDoctors() =>
            staffRepository.LoadAllStaff().OfType<Doctor>().ToList();

        public List<Appointment> GetAppointmentsByDoctor(int doctorId)
        {
            var allAppointments = Task.Run(() => appointmentRepository.GetAllAppointmentsAsync()).GetAwaiter().GetResult();
            bool IsConfirmedForDoctor(Appointment appointment) =>
                appointment.DoctorId == doctorId
                && string.Equals(appointment.Status, ConfirmedAppointmentStatus, StringComparison.OrdinalIgnoreCase);

            return allAppointments
                .Where(IsConfirmedForDoctor)
                .OrderBy(appointment => appointment.Date)
                .ThenBy(appointment => appointment.StartTime)
                .ToList();
        }

        public List<MedicalEvaluation> GetEvaluationsByDoctor(string doctorId)
        {
            if (!int.TryParse(doctorId, out var parsedDoctorId))
            {
                return new List<MedicalEvaluation>();
            }

            bool IsForDoctor(MedicalEvaluation evaluation) =>
                evaluation.Evaluator != null && evaluation.Evaluator.StaffID == parsedDoctorId;

            return evaluationsRepository.GetAllEvaluations()
                .Where(IsForDoctor)
                .OrderByDescending(evaluation => evaluation.EvaluationID)
                .ToList();
        }

        public void SaveEvaluation(MedicalEvaluation record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            int patientId = int.TryParse(record.PatientId, out var parsedPatientId) ? parsedPatientId : 0;
            bool assumedRisk = (record.Symptoms ?? string.Empty).IndexOf(RiskMarker, StringComparison.OrdinalIgnoreCase) >= 0;
            int doctorId = record.Evaluator?.StaffID ?? AppSettings.DefaultDoctorId;

            evaluationsRepository.AddEvaluation(
                doctorId,
                patientId,
                record.Symptoms ?? string.Empty,
                record.Notes ?? string.Empty,
                record.MedsList ?? string.Empty,
                assumedRisk);
        }

        public void DeleteEvaluation(int evaluationId) =>
            evaluationsRepository.DeleteEvaluation(evaluationId);

        public bool IsDoctorFatigued(string doctorId)
        {
            if (!int.TryParse(doctorId, out var parsedDoctorId))
            {
                return false;
            }

            DateTime lookbackStart = DateTime.Now.AddHours(-FatigueLookbackHours);
            double recentHours = shiftRepository.GetAllShifts()
                .Where(shift => shift.AppointedStaff.StaffID == parsedDoctorId && shift.EndTime >= lookbackStart)
                .Sum(shift => (shift.EndTime - shift.StartTime).TotalHours);

            return recentHours >= FatigueThresholdHours;
        }

        public string? CheckMedicineConflict(string patientId, string meds)
        {
            if (string.IsNullOrWhiteSpace(meds) || string.IsNullOrWhiteSpace(patientId))
            {
                return null;
            }

            string trimmedMedicineName = meds.Trim();
            var matchingMedicine = highRiskMedicineRepository.GetAllHighRiskMedicines()
                .FirstOrDefault(medicine => string.Equals(medicine.MedicineName, trimmedMedicineName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matchingMedicine.WarningMessage))
            {
                return matchingMedicine.WarningMessage;
            }

            return CheckPatientHistoryForRisk(patientId, meds);
        }

        private string? CheckPatientHistoryForRisk(string patientId, string currentMedicines)
        {
            bool MatchesPatient(MedicalEvaluation evaluation) =>
                string.Equals(evaluation.PatientId, patientId, StringComparison.OrdinalIgnoreCase);
            bool MentionsAllergyOrAdverse(MedicalEvaluation evaluation) =>
                ContainsKeyword(evaluation.Symptoms, AllergyKeyword)
                || ContainsKeyword(evaluation.Symptoms, AdverseKeyword)
                || ContainsKeyword(evaluation.Notes, AllergyKeyword)
                || ContainsKeyword(evaluation.Notes, AdverseKeyword);
            bool ListsSameMedicine(MedicalEvaluation evaluation) =>
                !string.IsNullOrEmpty(evaluation.MedsList)
                && evaluation.MedsList.Contains(currentMedicines, StringComparison.OrdinalIgnoreCase);

            var pastEvaluationWithMatch = evaluationsRepository.GetAllEvaluations()
                .Where(MatchesPatient)
                .Where(MentionsAllergyOrAdverse)
                .FirstOrDefault(ListsSameMedicine);

            return pastEvaluationWithMatch == null
                ? null
                : $"HISTORY ALERT: Patient had a past Adverse Reaction/Allergy to {currentMedicines} recorded in their history.";
        }

        private static bool ContainsKeyword(string? text, string keyword) =>
            !string.IsNullOrEmpty(text) && text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
