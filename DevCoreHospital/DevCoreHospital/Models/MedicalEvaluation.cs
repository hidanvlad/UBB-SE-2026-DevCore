using System;

namespace DevCoreHospital.Models;

public class MedicalEvaluation
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public DateTime EvaluationDate { get; set; }

    // REQUIRED by your MedicalDataService + MedicalEvaluationViewModel
    public string Evaluator { get; set; } = string.Empty;

    public string Symptoms { get; set; } = string.Empty;

    public string DiagnosisResult { get; set; } = string.Empty;

    public string MedsList { get; set; } = string.Empty;

    public string DoctorNotes { get; set; } = string.Empty;
}