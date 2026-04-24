using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Repositories;

public class EvaluationsRepositoryTests : IClassFixture<SqlTestFixture>
{
    private readonly SqlTestFixture database;
    private const string InvalidConnectionString = "Data Source=.;Initial Catalog=NoSuchDb;Integrated Security=True";

    public EvaluationsRepositoryTests(SqlTestFixture database) => this.database = database;


    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsNotNumeric_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository(database.ConnectionString).GetEvaluationsByDoctor("not-a-number"));

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsEmpty_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository(database.ConnectionString).GetEvaluationsByDoctor(string.Empty));

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsWhitespace_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository(database.ConnectionString).GetEvaluationsByDoctor("   "));

    [Fact]
    public void GetEvaluationsByDoctor_WhenDoctorIdIsAlphanumeric_ReturnsEmptyList()
        => Assert.Empty(new EvaluationsRepository(database.ConnectionString).GetEvaluationsByDoctor("DR-42"));

    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenMedsIsEmpty()
    {
        var repository = new EvaluationsRepository(database.ConnectionString);

        Assert.Null(repository.CheckMedicineConflict("P1", string.Empty));
    }

    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenMedsIsWhitespace()
    {
        var repository = new EvaluationsRepository(database.ConnectionString);

        Assert.Null(repository.CheckMedicineConflict("P1", "   "));
    }

    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenPatientIdIsEmpty()
    {
        var repository = new EvaluationsRepository(database.ConnectionString);

        Assert.Null(repository.CheckMedicineConflict(string.Empty, "Aspirin"));
    }

    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenPatientIdIsWhitespace()
    {
        var repository = new EvaluationsRepository(database.ConnectionString);

        Assert.Null(repository.CheckMedicineConflict("   ", "Aspirin"));
    }


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
        var repository = new TestableEvaluationsRepository(fatigueHours: 11.9);

        Assert.False(repository.IsDoctorFatigued("1"));
    }

    [Fact]
    public void IsDoctorFatigued_ReturnsTrue_WhenHoursAreExactlyAtThreshold()
    {
        var repository = new TestableEvaluationsRepository(fatigueHours: 12.0);

        Assert.True(repository.IsDoctorFatigued("1"));
    }

    [Fact]
    public void IsDoctorFatigued_ReturnsTrue_WhenHoursExceedThreshold()
    {
        var repository = new TestableEvaluationsRepository(fatigueHours: 16.5);

        Assert.True(repository.IsDoctorFatigued("1"));
    }


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
        var repository = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "ABC", Symptoms = "Fever", Notes = "N", MedsList = "M" };

        repository.SaveEvaluation(record);

        Assert.Equal(0, repository.CapturedPatientId);
    }

    [Fact]
    public void SaveEvaluation_ParsesPatientId_WhenNumeric()
    {
        var repository = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "42", Symptoms = "Cough", Notes = "N", MedsList = "M" };

        repository.SaveEvaluation(record);

        Assert.Equal(42, repository.CapturedPatientId);
    }

    [Fact]
    public void SaveEvaluation_SetsAssumedRiskTrue_WhenSymptomsContainRiskTag()
    {
        var repository = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "1", Symptoms = "Chest pain [RISK]", Notes = "N", MedsList = "M" };

        repository.SaveEvaluation(record);

        Assert.True(repository.CapturedAssumedRisk);
    }

    [Fact]
    public void SaveEvaluation_SetsAssumedRiskFalse_WhenSymptomsHaveNoRiskTag()
    {
        var repository = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "1", Symptoms = "Headache", Notes = "N", MedsList = "M" };

        repository.SaveEvaluation(record);

        Assert.False(repository.CapturedAssumedRisk);
    }

    [Fact]
    public void SaveEvaluation_SetsAssumedRiskTrue_WhenRiskTagIsMixedCase()
    {
        var repository = new CapturingSaveRepository();
        var record = new MedicalEvaluation { PatientId = "1", Symptoms = "Pain [risk]", Notes = "N", MedsList = "M" };

        repository.SaveEvaluation(record);

        Assert.True(repository.CapturedAssumedRisk);
    }

    [Fact]
    public void SaveEvaluation_UsesEvaluatorId_WhenEvaluatorIsSet()
    {
        var repository = new CapturingSaveRepository();
        var doctor = new Doctor { StaffID = 77 };
        var record = new MedicalEvaluation { PatientId = "1", Symptoms = "S", Notes = "N", MedsList = "M", Evaluator = doctor };

        repository.SaveEvaluation(record);

        Assert.Equal(77, repository.CapturedDoctorId);
    }


    [Fact]
    public void GetAllDoctors_ReturnsInsertedDoctor()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Eval", "AllDoc", "Neurology");
        try
        {
            var repository = new EvaluationsRepository(database.ConnectionString);

            var result = repository.GetAllDoctors();

            Assert.Contains(result, doctor => doctor.StaffID == doctorId);
        }
        finally
        {
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public void GetDoctorById_ReturnsDoctor_WhenExists()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "John", "EvalGet", "Cardiology");
        try
        {
            var repository = new EvaluationsRepository(database.ConnectionString);

            var result = repository.GetDoctorById(doctorId);

            Assert.NotNull(result);
            Assert.Equal(doctorId, result!.StaffID);
            Assert.Equal("John", result.FirstName);
        }
        finally
        {
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public void GetDoctorById_ReturnsNull_WhenNotFound()
    {
        var repository = new EvaluationsRepository(database.ConnectionString);

        var result = repository.GetDoctorById(int.MaxValue);

        Assert.Null(result);
    }


    [Fact]
    public void SaveEvaluation_ThenGetByDoctor_ReturnsInsertedEvaluation()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Save", "EvalDoc", "Pediatrics");
        try
        {
            var repository = new EvaluationsRepository(database.ConnectionString);
            var doctor = new Doctor { StaffID = doctorId };
            var record = new MedicalEvaluation
            {
                PatientId = "5",
                Symptoms = "Cough",
                Notes = "Rest advised",
                MedsList = "Paracetamol",
                Evaluator = doctor,
            };

            repository.SaveEvaluation(record);
            var results = repository.GetEvaluationsByDoctor(doctorId.ToString());

            Assert.Contains(results, evaluation => evaluation.Symptoms == "Cough" && evaluation.MedsList == "Paracetamol");
        }
        finally
        {
            database.DeleteMedicalEvaluationsByDoctor(connection, doctorId);
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public void DeleteEvaluation_RemovesEvaluation_SoItNoLongerAppears()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Del", "EvalDoc", "Orthopedics");
        try
        {
            var evalId = database.InsertMedicalEvaluation(connection, doctorId, 1, "Broken arm");
            var repository = new EvaluationsRepository(database.ConnectionString);

            repository.DeleteEvaluation(evalId);

            var remaining = repository.GetEvaluationsByDoctor(doctorId.ToString());
            Assert.DoesNotContain(remaining, evaluation => evaluation.EvaluationID == evalId);
        }
        finally
        {
            database.DeleteMedicalEvaluationsByDoctor(connection, doctorId);
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public void GetEvaluationsByDoctor_ReturnsMultipleEvaluations_WhenSeveralInserted()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Multi", "EvalDoc", "General");
        try
        {
            database.InsertMedicalEvaluation(connection, doctorId, 1, "Fever");
            database.InsertMedicalEvaluation(connection, doctorId, 2, "Cold");
            var repository = new EvaluationsRepository(database.ConnectionString);

            var results = repository.GetEvaluationsByDoctor(doctorId.ToString());

            Assert.True(results.Count >= 2);
        }
        finally
        {
            database.DeleteMedicalEvaluationsByDoctor(connection, doctorId);
            database.DeleteStaff(connection, doctorId);
        }
    }


    [Fact]
    public void GetAppointmentsByDoctor_ReturnsEmpty_WhenNoneExistForDoctor()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "NoApp", "EvalDoc", "Surgery");
        try
        {
            var repository = new EvaluationsRepository(database.ConnectionString);

            var result = repository.GetAppointmentsByDoctor(doctorId);

            Assert.Empty(result);
        }
        finally
        {
            database.DeleteStaff(connection, doctorId);
        }
    }

    [Fact]
    public void GetAppointmentsByDoctor_ReturnsConfirmedAppointment_WhenInserted()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "App", "EvalDoc", "ENT");
        int appointmentId = 0;
        try
        {
            var start = DateTime.Today.AddDays(5).AddHours(10);
            appointmentId = database.InsertAppointment(connection, 1, doctorId, start, start.AddHours(1), "Confirmed");
            var repository = new EvaluationsRepository(database.ConnectionString);

            var result = repository.GetAppointmentsByDoctor(doctorId);

            Assert.Single(result);
        }
        finally
        {
            if (appointmentId > 0)
            {
                database.DeleteAppointment(connection, appointmentId);
            }

            database.DeleteStaff(connection, doctorId);
        }
    }


    [Fact]
    public void CheckMedicineConflict_ReturnsNull_WhenNoHighRiskAndNoHistory()
    {
        var repository = new EvaluationsRepository(database.ConnectionString);

        var result = repository.CheckMedicineConflict("99999", "SomeSafeUnknownMed_XYZ");

        Assert.Null(result);
    }

    [Fact]
    public void CheckPatientHistoryForRisk_ReturnsNull_WhenNoHistoryFound()
    {
        var repository = new EvaluationsRepository(database.ConnectionString);

        var result = repository.CheckPatientHistoryForRisk("99999", "SafeMed");

        Assert.Null(result);
    }

    [Fact]
    public void CheckPatientHistoryForRisk_ReturnsAlert_WhenPatientHadAdverseReactionToSameMed()
    {
        using var connection = database.OpenConnection();
        var doctorId = database.InsertStaff(connection, "Doctor", "Risk", "HistDoc", "Allergy");
        int evalId = 0;
        try
        {
            evalId = database.InsertMedicalEvaluation(
                connection,
                doctorId,
                patientId: 98765,
                diagnosis: "Allergy to penicillin",
                notes: "Patient had Adverse reaction",
                meds: "Penicillin");
            var repository = new EvaluationsRepository(database.ConnectionString);

            var result = repository.CheckPatientHistoryForRisk("98765", "Penicillin");

            Assert.NotNull(result);
            Assert.Contains("HISTORY ALERT", result);
            Assert.Contains("Penicillin", result);
        }
        finally
        {
            if (evalId > 0)
            {
                database.DeleteMedicalEvaluation(connection, evalId);
            }

            database.DeleteStaff(connection, doctorId);
        }
    }
}
