using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Moq;

namespace DevCoreHospital.Tests.ViewModels
{
    public class AdminAppointmentsViewModelTests
    {
        private readonly Mock<IDoctorAppointmentService> mockService;
        private readonly AdminAppointmentsViewModel viewModel;

        public AdminAppointmentsViewModelTests()
        {
            mockService = new Mock<IDoctorAppointmentService>();
            viewModel = new AdminAppointmentsViewModel(mockService.Object);
        }


        [Fact]
        public async Task LoadDoctorsAsync_ResultsInEmptyCollection_WhenServiceReturnsNoDoctors()
        {
            mockService.Setup(appointmentService => appointmentService.GetAllDoctorsAsync())
                .ReturnsAsync(new List<(int DoctorId, string DoctorName)>());

            await viewModel.LoadDoctorsAsync();

            Assert.Empty(viewModel.Doctors);
        }

        [Fact]
        public async Task LoadDoctorsAsync_AddsDoctorToCollection_WhenServiceReturnsOneDoctor()
        {
            mockService.Setup(appointmentService => appointmentService.GetAllDoctorsAsync())
                .ReturnsAsync(new List<(int DoctorId, string DoctorName)> { (1, "Ana Pop") });

            await viewModel.LoadDoctorsAsync();

            Assert.Single(viewModel.Doctors);
        }

        [Fact]
        public async Task LoadDoctorsAsync_PreservesDoctorName_WhenDoctorNameIsNotEmpty()
        {
            mockService.Setup(appointmentService => appointmentService.GetAllDoctorsAsync())
                .ReturnsAsync(new List<(int DoctorId, string DoctorName)> { (1, "Ana Pop") });

            await viewModel.LoadDoctorsAsync();

            Assert.Equal("Ana Pop", viewModel.Doctors[0].DoctorName);
        }

        [Fact]
        public async Task LoadDoctorsAsync_UsesFallbackName_WhenDoctorNameIsEmpty()
        {
            mockService.Setup(appointmentService => appointmentService.GetAllDoctorsAsync())
                .ReturnsAsync(new List<(int DoctorId, string DoctorName)> { (7, "") });

            await viewModel.LoadDoctorsAsync();

            Assert.Equal("Doctor #7", viewModel.Doctors[0].DoctorName);
        }

        [Fact]
        public async Task LoadDoctorsAsync_UsesFallbackName_WhenDoctorNameIsWhitespace()
        {
            mockService.Setup(appointmentService => appointmentService.GetAllDoctorsAsync())
                .ReturnsAsync(new List<(int DoctorId, string DoctorName)> { (3, "   ") });

            await viewModel.LoadDoctorsAsync();

            Assert.Equal("Doctor #3", viewModel.Doctors[0].DoctorName);
        }

        [Fact]
        public async Task LoadDoctorsAsync_ClearsPreviousDoctors_WhenCalledASecondTime()
        {
            mockService.SetupSequence(appointmentService => appointmentService.GetAllDoctorsAsync())
                .ReturnsAsync(new List<(int DoctorId, string DoctorName)> { (1, "Ana Pop") })
                .ReturnsAsync(new List<(int DoctorId, string DoctorName)>());

            await viewModel.LoadDoctorsAsync();

            await viewModel.LoadDoctorsAsync();

            Assert.Empty(viewModel.Doctors);
        }


        [Fact]
        public async Task LoadAppointmentsForDoctorAsync_ResultsInEmptyList_WhenServiceReturnsNoAppointments()
        {
            mockService.Setup(appointmentService => appointmentService.GetAppointmentsForAdminAsync(1))
                .ReturnsAsync(new List<Appointment>());

            await viewModel.LoadAppointmentsForDoctorAsync(1);

            Assert.Empty(viewModel.AppointmentsList);
        }

        [Fact]
        public async Task LoadAppointmentsForDoctorAsync_AddsAppointmentToList_WhenServiceReturnsOneAppointment()
        {
            var appointment = new Appointment { Id = 10, DoctorId = 1, Status = "Scheduled" };
            mockService.Setup(appointmentService => appointmentService.GetAppointmentsForAdminAsync(1))
                .ReturnsAsync(new List<Appointment> { appointment });

            await viewModel.LoadAppointmentsForDoctorAsync(1);

            Assert.Single(viewModel.AppointmentsList);
        }

        [Fact]
        public async Task LoadAppointmentsForDoctorAsync_ClearsPreviousList_WhenCalledASecondTime()
        {
            var appointment = new Appointment { Id = 10, DoctorId = 1 };
            mockService.SetupSequence(appointmentService => appointmentService.GetAppointmentsForAdminAsync(1))
                .ReturnsAsync(new List<Appointment> { appointment })
                .ReturnsAsync(new List<Appointment>());

            await viewModel.LoadAppointmentsForDoctorAsync(1);

            await viewModel.LoadAppointmentsForDoctorAsync(1);

            Assert.Empty(viewModel.AppointmentsList);
        }


        [Fact]
        public async Task BookAppointmentAsync_CallsCreateAppointmentAsync_WithCorrectPatientAndDoctor()
        {
            var date = new DateTime(2025, 8, 1);
            var time = new TimeSpan(9, 0, 0);

            await viewModel.BookAppointmentAsync("PAT-42", 5, date, time);

            mockService.Verify(appointmentService => appointmentService.CreateAppointmentAsync("PAT-42", 5, date, time), Times.Once);
        }

        [Fact]
        public async Task BookAppointmentAsync_PassesDateAndTimeDirectlyToService()
        {
            var dateWithTime = new DateTime(2025, 8, 1, 15, 30, 0);
            var time = new TimeSpan(10, 0, 0);

            await viewModel.BookAppointmentAsync("PAT-1", 1, dateWithTime, time);

            mockService.Verify(appointmentService => appointmentService.CreateAppointmentAsync("PAT-1", 1, dateWithTime, time), Times.Once);
        }


        [Fact]
        public async Task FinishAppointmentAsync_DelegatesToService_WithGivenAppointment()
        {
            var appointment = new Appointment { Id = 5, Status = "Scheduled" };

            await viewModel.FinishAppointmentAsync(appointment);

            mockService.Verify(appointmentService => appointmentService.FinishAppointmentAsync(appointment), Times.Once);
        }


        [Fact]
        public async Task CancelAppointmentAsync_DelegatesToService_WithGivenAppointment()
        {
            var appointment = new Appointment { Id = 5, Status = "Scheduled" };

            await viewModel.CancelAppointmentAsync(appointment);

            mockService.Verify(appointmentService => appointmentService.CancelAppointmentAsync(appointment), Times.Once);
        }
    }
}
