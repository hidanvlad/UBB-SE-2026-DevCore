using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class MedicalEvaluationServiceTests
    {
        private readonly Mock<IEvaluationsRepository> evaluationsRepository = new();
        private readonly Mock<IHighRiskMedicineRepository> highRiskMedicineRepository = new();
        private readonly Mock<IAppointmentRepository> appointmentRepository = new();
        private readonly Mock<IStaffRepository> staffRepository = new();
        private readonly Mock<IShiftRepository> shiftRepository = new();

        public MedicalEvaluationServiceTests()
        {
            evaluationsRepository.Setup(repository => repository.GetAllEvaluations())
                .Returns(new List<MedicalEvaluation>());
            highRiskMedicineRepository.Setup(repository => repository.GetAllHighRiskMedicines())
                .Returns(new List<(string MedicineName, string WarningMessage)>());
            appointmentRepository.Setup(repository => repository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment>());
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff>());
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>());
        }

        private MedicalEvaluationService CreateService() =>
            new MedicalEvaluationService(
                evaluationsRepository.Object,
                highRiskMedicineRepository.Object,
                appointmentRepository.Object,
                staffRepository.Object,
                shiftRepository.Object);

        [Fact]
        public void GetAllDoctors_ReturnsDoctorsFromStaffRepository()
        {
            var doctor = new Doctor(1, "Ana", "Pop", string.Empty, true, "Cardiology", "LIC", DoctorStatus.AVAILABLE, 3);
            var pharmacist = new Pharmacyst(2, "Test", "Staff", string.Empty, true, "General", 1);
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff> { doctor, pharmacist });

            var result = CreateService().GetAllDoctors();

            Assert.Single(result);
            Assert.Equal(1, result[0].StaffID);
        }

        [Fact]
        public void GetAppointmentsByDoctor_ReturnsConfirmedAppointmentsForDoctor()
        {
            var matching = new Appointment { Id = 5, DoctorId = 10, Status = "Confirmed" };
            var otherDoctor = new Appointment { Id = 6, DoctorId = 11, Status = "Confirmed" };
            var notConfirmed = new Appointment { Id = 7, DoctorId = 10, Status = "Scheduled" };
            appointmentRepository.Setup(repository => repository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment> { matching, otherDoctor, notConfirmed });

            var result = CreateService().GetAppointmentsByDoctor(10);

            Assert.Single(result);
            Assert.Equal(5, result[0].Id);
        }

        [Fact]
        public void GetEvaluationsByDoctor_FiltersByEvaluatorStaffId()
        {
            var matching = new MedicalEvaluation { EvaluationID = 1, Evaluator = new Doctor { StaffID = 10 } };
            var notMatching = new MedicalEvaluation { EvaluationID = 2, Evaluator = new Doctor { StaffID = 11 } };
            evaluationsRepository.Setup(repository => repository.GetAllEvaluations())
                .Returns(new List<MedicalEvaluation> { matching, notMatching });

            var result = CreateService().GetEvaluationsByDoctor("10");

            Assert.Single(result);
            Assert.Equal(1, result[0].EvaluationID);
        }

        [Fact]
        public void GetEvaluationsByDoctor_ReturnsEmpty_WhenDoctorIdNotParseable()
        {
            var result = CreateService().GetEvaluationsByDoctor("not-a-number");

            Assert.Empty(result);
        }

        [Fact]
        public void SaveEvaluation_DelegatesToRepository()
        {
            var evaluation = new MedicalEvaluation { PatientId = "42", Evaluator = new Doctor { StaffID = 7 } };

            CreateService().SaveEvaluation(evaluation);

            evaluationsRepository.Verify(repository => repository.AddEvaluation(
                7,
                42,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public void DeleteEvaluation_DelegatesToRepository()
        {
            CreateService().DeleteEvaluation(42);

            evaluationsRepository.Verify(repository => repository.DeleteEvaluation(42), Times.Once);
        }

        [Fact]
        public void IsDoctorFatigued_ReturnsTrue_WhenRecentShiftHoursExceedThreshold()
        {
            var staff = new Doctor { StaffID = 5 };
            var recentShift = new Shift(1, staff, "Ward A", DateTime.Now.AddHours(-15), DateTime.Now.AddHours(-2), ShiftStatus.SCHEDULED);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { recentShift });

            var result = CreateService().IsDoctorFatigued("5");

            Assert.True(result);
        }

        [Fact]
        public void IsDoctorFatigued_ReturnsFalse_WhenRecentHoursBelowThreshold()
        {
            var staff = new Doctor { StaffID = 3 };
            var recentShift = new Shift(1, staff, "Ward A", DateTime.Now.AddHours(-3), DateTime.Now.AddHours(-1), ShiftStatus.SCHEDULED);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { recentShift });

            var result = CreateService().IsDoctorFatigued("3");

            Assert.False(result);
        }

        [Fact]
        public void CheckMedicineConflict_ReturnsWarning_WhenMedicineIsHighRisk()
        {
            highRiskMedicineRepository.Setup(repository => repository.GetAllHighRiskMedicines())
                .Returns(new List<(string MedicineName, string WarningMessage)> { ("Aspirin", "Risk: bleeding") });

            var result = CreateService().CheckMedicineConflict("P1", "Aspirin");

            Assert.Equal("Risk: bleeding", result);
        }

        [Fact]
        public void CheckMedicineConflict_ReturnsNull_WhenNoConflict()
        {
            var result = CreateService().CheckMedicineConflict("P2", "Ibuprofen");

            Assert.Null(result);
        }

        [Fact]
        public void CheckMedicineConflict_ReturnsHistoryAlert_WhenPatientHadAllergyToSameMed()
        {
            var pastEvaluation = new MedicalEvaluation
            {
                PatientId = "P1",
                Symptoms = "Allergy reported",
                MedsList = "Penicillin",
            };
            evaluationsRepository.Setup(repository => repository.GetAllEvaluations())
                .Returns(new List<MedicalEvaluation> { pastEvaluation });

            var result = CreateService().CheckMedicineConflict("P1", "Penicillin");

            Assert.NotNull(result);
            Assert.Contains("HISTORY ALERT", result);
        }
    }
}
