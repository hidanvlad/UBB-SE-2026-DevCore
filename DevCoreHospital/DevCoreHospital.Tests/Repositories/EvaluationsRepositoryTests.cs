using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class EvaluationsRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture db;
    private const string InvalidConnectionString = "Data Source=.;Initial Catalog=NoSuchDb;Integrated Security=True";

    public EvaluationsRepositoryTests(SqlTestFixture db) => this.db = db;

    // -----------------------------------------------------------------------
    // Guard-clause tests — no DB needed
    // -----------------------------------------------------------------------

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsNotNumeric_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository(db.ConnectionString).GetEvaluationsByDoctor("not-a-number"));

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsEmpty_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository(db.ConnectionString).GetEvaluationsByDoctor(string.Empty));

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsWhitespace_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository(db.ConnectionString).GetEvaluationsByDoctor("   "));

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsAlphanumeric_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository(db.ConnectionString).GetEvaluationsByDoctor("DR-42"));

    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenMedsIsEmpty()
    {
        var repo = new EvaluationsRepository(db.ConnectionString);

        Assert.Null(repo.CheckMedicineConflict("P1", string.Empty));
    }

    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenMedsIsWhitespace()
    {
        var repo = new EvaluationsRepository(db.ConnectionString);

        Assert.Null(repo.CheckMedicineConflict("P1", "   "));
    }

    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenPatientIdIsEmpty()
    {
        var repo = new EvaluationsRepository(db.ConnectionString);

        Assert.Null(repo.CheckMedicineConflict(string.Empty, "Aspirin"));
    }

    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenPatientIdIsWhitespace()
    {
        var repo = new EvaluationsRepository(db.ConnectionString);

        Assert.Null(repo.CheckMedicineConflict("   ", "Aspirin"));
    }

    // -----------------------------------------------------------------------
    // IsDoctorFatigued — uses testable subclass (pure logic, threshold = 12h)
    // -----------------------------------------------------------------------

    private sealed class TestableEvaluationsRepository : EvaluationsRepository
    {
        private readonly double fatigueHours;

        public TestableEvaluationsRepository(double fatigueHours) : base("fake")
            => this.fatigueHours = fatigueHours;

        public override double GetDoctorFatigueHours(string doctorId) => fatigueHours;
    }

    [Fact]
    public void IsDoctorFatigued_ReturnsFalse_WhenHoursAreBelowThreshold()
    {
        var repo = new TestableEvaluationsRepository(fatigueHours: 11.9);

        Assert.False(repo.IsDoctorFatigued("1"));
    }

    [Fact]
    public void IsDoctorFatigued_ReturnsTrue_WhenHoursAreExactlyAtThreshold()
    {
        var repo = new TestableEvaluationsRepository(fatigueHours: 12.0);

        Assert.True(repo.IsDoctorFatigued("1"));
    }

    [Fact]
    public void IsDoctorFatigued_ReturnsTrue_WhenHoursExceedThreshold()
    {
        var repo = new TestableEvaluationsRepository(fatigueHours: 16.5);

        Assert.True(repo.IsDoctorFatigued("1"));
    }

    // -----------------------------------------------------------------------
    // SaveEvaluation — uses testable subclass to verify parsed arguments
    // -----------------------------------------------------------------------

    private sealed class CapturingSaveRepository : EvaluationsRepository
    {
        public int CapturedDoctorId { get; private set; }
        public int CapturedPatientId { get; private set; }
        public string CapturedDiagnosis { get; private set; } = string.Empty;
        public bool CapturedAssumedRisk { get; private set; }

        public CapturingSaveRepository() : base("fake") { }

        protected override void ExecuteSaveEvaluation(
            int doctorId, int patientId, string diagnosis, string notes, string meds, bool assumedRisk)
        {
            CapturedDoctorId = doctorId;
            CapturedPatientId = patientId;
            CapturedDiagnosis = diagnosis;
            CapturedAssumedRisk = assumedRisk;
        }
    }

    [Fact]
    public void SaveEvaluation_UsesZeroPatientId_WhenPatientIdIsNonNumeric()
    {
        var repo = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "ABC", Symptoms = "Fever", Notes = "N", MedsList = "M" };

        repo.SaveEvaluation(record);

        Assert.Equal(0, repo.CapturedPatientId);
    }

    [Fact]
    public void SaveEvaluation_ParsesPatientId_WhenNumeric()
    {
        var repo = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "42", Symptoms = "Cough", Notes = "N", MedsList = "M" };

        repo.SaveEvaluation(record);

        Assert.Equal(42, repo.CapturedPatientId);
    }

    [Fact]
    public void SaveEvaluation_SetsAssumedRiskTrue_WhenSymptomsContainRiskTag()
    {
        var repo = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "1", Symptoms = "Chest pain [RISK]", Notes = "N", MedsList = "M" };

        repo.SaveEvaluation(record);

        Assert.True(repo.CapturedAssumedRisk);
    }

    [Fact]
    public void SaveEvaluation_SetsAssumedRiskFalse_WhenSymptomsHaveNoRiskTag()
    {
        var repo = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "1", Symptoms = "Headache", Notes = "N", MedsList = "M" };

        repo.SaveEvaluation(record);

        Assert.False(repo.CapturedAssumedRisk);
    }

    [Fact]
    public void SaveEvaluation_SetsAssumedRiskTrue_WhenRiskTagIsMixedCase()
    {
        var repo = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "1", Symptoms = "Pain [risk]", Notes = "N", MedsList = "M" };

        repo.SaveEvaluation(record);

        Assert.True(repo.CapturedAssumedRisk);
    }

    [Fact]
    public void SaveEvaluation_UsesEvaluatorId_WhenEvaluatorIsSet()
    {
        var repo = new CapturingSaveRepository();
        var doctor = new Doctor { StaffID = 77 };
        var record = new MedicalEvaluation { PatientId = "1", Symptoms = "S", Notes = "N", MedsList = "M", Evaluator = doctor };

        repo.SaveEvaluation(record);

        Assert.Equal(77, repo.CapturedDoctorId);
    }

    // -----------------------------------------------------------------------
    // DB integration: GetAllDoctors / GetDoctorById
    // -----------------------------------------------------------------------

    [Fact]
    public void GetAllDoctors_ReturnsInsertedDoctor()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Eval", "AllDoc", "Neurology");
        try
        {
            var repo = new EvaluationsRepository(db.ConnectionString);

            var result = repo.GetAllDoctors();

            Assert.Contains(result, d => d.StaffID == doctorId);
        }
        finally
        {
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public void GetDoctorById_ReturnsDoctor_WhenExists()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "John", "EvalGet", "Cardiology");
        try
        {
            var repo = new EvaluationsRepository(db.ConnectionString);

            var result = repo.GetDoctorById(doctorId);

            Assert.NotNull(result);
            Assert.Equal(doctorId, result!.StaffID);
            Assert.Equal("John", result.FirstName);
        }
        finally
        {
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public void GetDoctorById_ReturnsNull_WhenNotFound()
    {
        var repo = new EvaluationsRepository(db.ConnectionString);

        var result = repo.GetDoctorById(int.MaxValue);

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // DB integration: SaveEvaluation + GetEvaluationsByDoctor + DeleteEvaluation
    // -----------------------------------------------------------------------

    [Fact]
    public void SaveEvaluation_ThenGetByDoctor_ReturnsInsertedEvaluation()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Save", "EvalDoc", "Pediatrics");
        try
        {
            var repo = new EvaluationsRepository(db.ConnectionString);
            var doctor = new Doctor { StaffID = doctorId };
            var record = new MedicalEvaluation
            {
                PatientId = "5",
                Symptoms = "Cough",
                Notes = "Rest advised",
                MedsList = "Paracetamol",
                Evaluator = doctor,
            };

            repo.SaveEvaluation(record);
            var results = repo.GetEvaluationsByDoctor(doctorId.ToString());

            Assert.Contains(results, e => e.Symptoms == "Cough" && e.MedsList == "Paracetamol");
        }
        finally
        {
            db.DeleteMedicalEvaluationsByDoctor(conn, doctorId);
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public void DeleteEvaluation_RemovesEvaluation_SoItNoLongerAppears()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Del", "EvalDoc", "Orthopedics");
        try
        {
            var evalId = db.InsertMedicalEvaluation(conn, doctorId, 1, "Broken arm");
            var repo = new EvaluationsRepository(db.ConnectionString);

            repo.DeleteEvaluation(evalId);

            var remaining = repo.GetEvaluationsByDoctor(doctorId.ToString());
            Assert.DoesNotContain(remaining, e => e.EvaluationID == evalId);
        }
        finally
        {
            db.DeleteMedicalEvaluationsByDoctor(conn, doctorId);
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public void GetEvaluationsByDoctor_ReturnsMultipleEvaluations_WhenSeveralInserted()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Multi", "EvalDoc", "General");
        try
        {
            db.InsertMedicalEvaluation(conn, doctorId, 1, "Fever");
            db.InsertMedicalEvaluation(conn, doctorId, 2, "Cold");
            var repo = new EvaluationsRepository(db.ConnectionString);

            var results = repo.GetEvaluationsByDoctor(doctorId.ToString());

            Assert.True(results.Count >= 2);
        }
        finally
        {
            db.DeleteMedicalEvaluationsByDoctor(conn, doctorId);
            db.DeleteStaff(conn, doctorId);
        }
    }

    // -----------------------------------------------------------------------
    // DB integration: GetAppointmentsByDoctor
    // -----------------------------------------------------------------------

    [Fact]
    public void GetAppointmentsByDoctor_ReturnsEmpty_WhenNoneExistForDoctor()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "NoApp", "EvalDoc", "Surgery");
        try
        {
            var repo = new EvaluationsRepository(db.ConnectionString);

            var result = repo.GetAppointmentsByDoctor(doctorId);

            Assert.Empty(result);
        }
        finally
        {
            db.DeleteStaff(conn, doctorId);
        }
    }

    [Fact]
    public void GetAppointmentsByDoctor_ReturnsConfirmedAppointment_WhenInserted()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "App", "EvalDoc", "ENT");
        int appointmentId = 0;
        try
        {
            var start = DateTime.Today.AddDays(5).AddHours(10);
            appointmentId = db.InsertAppointment(conn, 1, doctorId, start, start.AddHours(1), "Confirmed");
            var repo = new EvaluationsRepository(db.ConnectionString);

            var result = repo.GetAppointmentsByDoctor(doctorId);

            Assert.Single(result);
        }
        finally
        {
            if (appointmentId > 0)
            {
                db.DeleteAppointment(conn, appointmentId);
            }

            db.DeleteStaff(conn, doctorId);
        }
    }

    // -----------------------------------------------------------------------
    // DB integration: CheckMedicineConflict / CheckPatientHistoryForRisk
    // -----------------------------------------------------------------------

    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenNoHighRiskAndNoHistory()
    {
        var repo = new EvaluationsRepository(db.ConnectionString);

        var result = repo.CheckMedicineConflict("99999", "SomeSafeUnknownMed_XYZ");

        Assert.Null(result);
    }

    [Fact]
    public void CheckPatientHistoryForRisk_ReturnsNull_WhenNoHistoryFound()
    {
        var repo = new EvaluationsRepository(db.ConnectionString);

        var result = repo.CheckPatientHistoryForRisk("99999", "SafeMed");

        Assert.Null(result);
    }

    [Fact]
    public void CheckPatientHistoryForRisk_ReturnsAlert_WhenPatientHadAdverseReactionToSameMed()
    {
        using var conn = db.OpenConnection();
        var doctorId = db.InsertStaff(conn, "Doctor", "Risk", "HistDoc", "Allergy");
        int evalId = 0;
        try
        {
            evalId = db.InsertMedicalEvaluation(
                conn,
                doctorId,
                patientId: 98765,
                diagnosis: "Allergy to penicillin",
                notes: "Patient had Adverse reaction",
                meds: "Penicillin");
            var repo = new EvaluationsRepository(db.ConnectionString);

            var result = repo.CheckPatientHistoryForRisk("98765", "Penicillin");

            Assert.NotNull(result);
            Assert.Contains("HISTORY ALERT", result);
            Assert.Contains("Penicillin", result);
        }
        finally
        {
            if (evalId > 0)
            {
                db.DeleteMedicalEvaluation(conn, evalId);
            }

            db.DeleteStaff(conn, doctorId);
        }
    }
}
