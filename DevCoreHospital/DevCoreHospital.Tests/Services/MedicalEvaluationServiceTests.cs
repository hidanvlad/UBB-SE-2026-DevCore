using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class MedicalEvaluationServiceTests
    {
        private readonly Mock<IEvaluationsRepository> repositoryMock;
        private readonly MedicalEvaluationService sut;

        public MedicalEvaluationServiceTests()
        {
            repositoryMock = new Mock<IEvaluationsRepository>();
            sut = new MedicalEvaluationService(repositoryMock.Object);
        }

        [Fact]
        public void GetAllDoctors_DelegatesToRepository()
        {
            var doctor = new Doctor(1, "Ana", "Pop", string.Empty, string.Empty, true, "Cardiology", "AVAILABLE", DoctorStatus.AVAILABLE, 3);
            repositoryMock.Setup(r => r.GetAllDoctors()).Returns(new List<Doctor> { doctor });

            var result = sut.GetAllDoctors();

            Assert.Single(result);
            Assert.Equal(1, result[0].StaffID);
            repositoryMock.Verify(r => r.GetAllDoctors(), Times.Once);
        }

        [Fact]
        public void GetAppointmentsByDoctor_DelegatesToRepository_WithCorrectId()
        {
            var appointment = new Appointment { Id = 5, DoctorId = 10 };
            repositoryMock.Setup(r => r.GetAppointmentsByDoctor(10)).Returns(new List<Appointment> { appointment });

            var result = sut.GetAppointmentsByDoctor(10);

            Assert.Single(result);
            Assert.Equal(5, result[0].Id);
            repositoryMock.Verify(r => r.GetAppointmentsByDoctor(10), Times.Once);
        }

        [Fact]
        public void GetEvaluationsByDoctor_DelegatesToRepository_WithCorrectDoctorId()
        {
            var evaluation = new MedicalEvaluation { PatientId = "P1" };
            repositoryMock.Setup(r => r.GetEvaluationsByDoctor("10")).Returns(new List<MedicalEvaluation> { evaluation });

            var result = sut.GetEvaluationsByDoctor("10");

            Assert.Single(result);
            repositoryMock.Verify(r => r.GetEvaluationsByDoctor("10"), Times.Once);
        }

        [Fact]
        public void SaveEvaluation_DelegatesToRepository()
        {
            var evaluation = new MedicalEvaluation { PatientId = "P2" };

            sut.SaveEvaluation(evaluation);

            repositoryMock.Verify(r => r.SaveEvaluation(evaluation), Times.Once);
        }

        [Fact]
        public void DeleteEvaluation_DelegatesToRepository_WithCorrectId()
        {
            sut.DeleteEvaluation(42);

            repositoryMock.Verify(r => r.DeleteEvaluation(42), Times.Once);
        }

        [Fact]
        public void IsDoctorFatigued_ReturnsTrue_WhenRepositoryReturnsTrue()
        {
            repositoryMock.Setup(r => r.IsDoctorFatigued("5")).Returns(true);

            var result = sut.IsDoctorFatigued("5");

            Assert.True(result);
            repositoryMock.Verify(r => r.IsDoctorFatigued("5"), Times.Once);
        }

        [Fact]
        public void IsDoctorFatigued_ReturnsFalse_WhenRepositoryReturnsFalse()
        {
            repositoryMock.Setup(r => r.IsDoctorFatigued("3")).Returns(false);

            var result = sut.IsDoctorFatigued("3");

            Assert.False(result);
        }

        [Fact]
        public void CheckMedicineConflict_ReturnsWarning_WhenConflictExists()
        {
            repositoryMock.Setup(r => r.CheckMedicineConflict("P1", "Aspirin")).Returns("Risk: bleeding");

            var result = sut.CheckMedicineConflict("P1", "Aspirin");

            Assert.Equal("Risk: bleeding", result);
            repositoryMock.Verify(r => r.CheckMedicineConflict("P1", "Aspirin"), Times.Once);
        }

        [Fact]
        public void CheckMedicineConflict_ReturnsNull_WhenNoConflict()
        {
            repositoryMock.Setup(r => r.CheckMedicineConflict("P2", "Ibuprofen")).Returns((string?)null);

            var result = sut.CheckMedicineConflict("P2", "Ibuprofen");

            Assert.Null(result);
        }
    }
}
