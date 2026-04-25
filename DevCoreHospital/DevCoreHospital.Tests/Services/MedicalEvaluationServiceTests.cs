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

            var doctors = CreateService().GetAllDoctors();

            Assert.Single(doctors);
            Assert.Equal(1, doctors[0].StaffID);
        }

        [Fact]
        public void GetAppointmentsByDoctor_ReturnsConfirmedAppointmentsForDoctor()
        {
            var matching = new Appointment { Id = 5, DoctorId = 10, Status = "Confirmed" };
            var otherDoctor = new Appointment { Id = 6, DoctorId = 11, Status = "Confirmed" };
            var notConfirmed = new Appointment { Id = 7, DoctorId = 10, Status = "Scheduled" };
            appointmentRepository.Setup(repository => repository.GetAllAppointmentsAsync())
                .ReturnsAsync(new List<Appointment> { matching, otherDoctor, notConfirmed });

            var appointments = CreateService().GetAppointmentsByDoctor(10);

            Assert.Single(appointments);
            Assert.Equal(5, appointments[0].Id);
        }

        [Fact]
        public void GetEvaluationsByDoctor_FiltersByEvaluatorStaffId()
        {
            var matching = new MedicalEvaluation { EvaluationID = 1, Evaluator = new Doctor { StaffID = 10 } };
            var notMatching = new MedicalEvaluation { EvaluationID = 2, Evaluator = new Doctor { StaffID = 11 } };
            evaluationsRepository.Setup(repository => repository.GetAllEvaluations())
                .Returns(new List<MedicalEvaluation> { matching, notMatching });

            var evaluations = CreateService().GetEvaluationsByDoctor("10");

            Assert.Single(evaluations);
            Assert.Equal(1, evaluations[0].EvaluationID);
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

            var isFatigued = CreateService().IsDoctorFatigued("5");

            Assert.True(isFatigued);
        }

        [Fact]
        public void IsDoctorFatigued_ReturnsFalse_WhenRecentHoursBelowThreshold()
        {
            var staff = new Doctor { StaffID = 3 };
            var recentShift = new Shift(1, staff, "Ward A", DateTime.Now.AddHours(-3), DateTime.Now.AddHours(-1), ShiftStatus.SCHEDULED);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { recentShift });

            var isFatigued = CreateService().IsDoctorFatigued("3");

            Assert.False(isFatigued);
        }

        [Fact]
        public void CheckMedicineConflict_ReturnsWarning_WhenMedicineIsHighRisk()
        {
            highRiskMedicineRepository.Setup(repository => repository.GetAllHighRiskMedicines())
                .Returns(new List<(string MedicineName, string WarningMessage)> { ("Aspirin", "Risk: bleeding") });

            var medicineConflict = CreateService().CheckMedicineConflict("P1", "Aspirin");

            Assert.Equal("Risk: bleeding", medicineConflict);
        }

        [Fact]
        public void CheckMedicineConflict_ReturnsNull_WhenNoConflict()
        {
            var medicineConflict = CreateService().CheckMedicineConflict("P2", "Ibuprofen");

            Assert.Null(medicineConflict);
        }

        [Fact]
        public void CheckMedicineConflict_WhenPatientHadAllergyToSameMed_ReturnsHistoryAlert()
        {
            var pastEvaluation = new MedicalEvaluation
            {
                PatientId = "P1",
                Symptoms = "Allergy reported",
                MedicationsList = "Penicillin",
            };
            evaluationsRepository.Setup(repository => repository.GetAllEvaluations())
                .Returns(new List<MedicalEvaluation> { pastEvaluation });

            var medicineConflict = CreateService().CheckMedicineConflict("P1", "Penicillin");

            Assert.Contains("HISTORY ALERT", medicineConflict ?? string.Empty);
        }
    }
}
