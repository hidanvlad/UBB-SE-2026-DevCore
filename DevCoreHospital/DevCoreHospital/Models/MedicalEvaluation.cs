using System;

namespace DevCoreHospital.Models
{
    public class MedicalEvaluation
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimeFormat = "HH:mm";

        public int EvaluationID { get; set; }
        public string PatientId { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string MedicationsList { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime EvaluationDate { get; set; }
        public Doctor? Evaluator { get; set; }

        public string FormattedDate => EvaluationDate.ToString(DateFormat);
        public string FormattedTime => EvaluationDate.ToString(TimeFormat);
    }
}