using System;

namespace DevCoreHospital.Models
{
    public class MedicalEvaluation
    {

        public int EvaluationID { get; set; }

        public string PatientId { get; set; } = string.Empty;
        public string DiagnosisResult { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string MedsList { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public DateTime EvaluationDate { get; set; }
        public Doctor? Evaluator { get; set; }

        public string FormattedDate => EvaluationDate.ToString("dd MMM yyyy");
        public string FormattedTime => EvaluationDate.ToString("HH:mm");
    }
}