using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using DevCoreHospital.ViewModels.Doctor;
using DevCoreHospital.ViewModels.Pharmacy;
using DevCoreHospital.Views.Shell;
using Moq;

namespace DevCoreHospital.Tests.Integration
{
    public class AppointmentFlowIntegrationTests
    {
        private sealed class InMemoryAppointmentDataSource : IAppointmentRepository
        {
            private readonly List<Appointment> appointments = new();

            public IReadOnlyList<Appointment> AllAppointments => appointments.AsReadOnly();

            public Task AddAppointmentAsync(int patientId, int doctorId, DateTime startTime, DateTime endTime, string status)
            {
                appointments.Add(new Appointment
                {
                    Id = appointments.Count + 1,
                    PatientName = patientId.ToString(),
                    DoctorId = doctorId,
                    Date = startTime.Date,
                    StartTime = startTime.TimeOfDay,
                    EndTime = endTime.TimeOfDay,
                    Status = status,
                });
                return Task.CompletedTask;
            }

            public Task UpdateAppointmentStatusAsync(int id, string status)
            {
                bool HasMatchingId(Appointment appointment) => appointment.Id == id;
                var appointment = appointments.FirstOrDefault(HasMatchingId);
                if (appointment != null)
                {
                    appointment.Status = status;
                }

                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<Appointment>> GetAllAppointmentsAsync()
            {
                IReadOnlyList<Appointment> result = appointments.ToList();
                return Task.FromResult(result);
            }
        }

        private sealed class InMemoryStaffRepository : IStaffRepository
        {
            private readonly Dictionary<int, string> staffStatuses = new();
            private readonly List<(int DoctorId, string FirstName, string LastName)> doctors;

            public InMemoryStaffRepository(List<(int DoctorId, string FirstName, string LastName)>? doctors = null)
                => this.doctors = doctors ?? new List<(int, string, string)>();

            public string GetStatus(int staffId) =>
                staffStatuses.TryGetValue(staffId, out var status) ? status : string.Empty;

            public List<IStaff> LoadAllStaff() => new();

            public IStaff? GetStaffById(int staffId) => null;

            public Task<IReadOnlyList<(int DoctorId, string FirstName, string LastName)>> GetAllDoctorsAsync()
            {
                IReadOnlyList<(int DoctorId, string FirstName, string LastName)> result = doctors;
                return Task.FromResult(result);
            }

            public Task UpdateStatusAsync(int staffId, string status)
            {
                staffStatuses[staffId] = status;
                return Task.CompletedTask;
            }
        }

        private sealed class InMemoryPharmacyStaffRepository : IPharmacyStaffRepository
        {
            private readonly List<Pharmacyst> pharmacists;

            public InMemoryPharmacyStaffRepository(List<Pharmacyst> pharmacists)
                => this.pharmacists = pharmacists;

            public List<Pharmacyst> GetPharmacists() => pharmacists;
        }

        private sealed class InMemoryPharmacyShiftRepository : IPharmacyShiftRepository
        {
            private readonly List<Shift> shifts = new();

            public IReadOnlyList<Shift> GetAllShifts() => shifts;

            public void AddShift(Shift shift) => shifts.Add(shift);
        }

        private sealed class InMemoryShiftRepository : IShiftRepository
        {
            private readonly List<Shift> shifts;

            public InMemoryShiftRepository(List<Shift> shifts) => this.shifts = shifts;

            public IReadOnlyList<Shift> GetAllShifts() => shifts;

            public void AddShift(Shift newShift) => shifts.Add(newShift);

            public void UpdateShiftStatus(int shiftId, ShiftStatus status) { }

            public void UpdateShiftStaffId(int shiftId, int newStaffId) { }

            public void DeleteShift(int shiftId) { }
        }


        private static readonly Pharmacyst TestPharmacist =
            new Pharmacyst(1, "Ana", "Pop", string.Empty, true, "General", 3);

        private static Appointment MakeAppointment() => new Appointment
        {
            Id = 1,
            DoctorId = 10,
            PatientName = "PAT-001",
            Date = new DateTime(2025, 8, 1),
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(9, 30, 0),
            Status = "Scheduled",
        };

        private static (DoctorScheduleViewModel ViewModel, InMemoryAppointmentDataSource DataSource) CreateScheduleStack(
            List<Shift>? shifts = null)
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var staffRepository = new InMemoryStaffRepository();
            var shiftRepository = new InMemoryShiftRepository(shifts ?? new List<Shift>());
            var mockUser = new Mock<ICurrentUserService>();
            mockUser.Setup(currentUser => currentUser.Role).Returns("Doctor");
            mockUser.Setup(currentUser => currentUser.UserId).Returns(1);
            var service = new DoctorAppointmentService(dataSource, staffRepository, shiftRepository);
            var viewModel = new DoctorScheduleViewModel(
                mockUser.Object,
                service,
                new DialogPresenter());
            return (viewModel, dataSource);
        }


        [Fact]
        public async Task BookThenFinish_AppointmentStatusIsFinished()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var staffRepository = new InMemoryStaffRepository();
            var service = new DoctorAppointmentService(dataSource, staffRepository);
            var appointment = MakeAppointment();

            await service.BookAppointmentAsync(appointment);
            await service.FinishAppointmentAsync(appointment);

            Assert.Equal("Finished", dataSource.AllAppointments[0].Status);
        }

        [Fact]
        public async Task BookThenFinish_DoctorStatusIsAvailable_WhenLastActiveAppointmentDone()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var staffRepository = new InMemoryStaffRepository();
            var service = new DoctorAppointmentService(dataSource, staffRepository);
            var appointment = MakeAppointment();

            await service.BookAppointmentAsync(appointment);
            await service.FinishAppointmentAsync(appointment);

            Assert.Equal("AVAILABLE", staffRepository.GetStatus(10));
        }

        [Fact]
        public async Task BookThenFinish_DoctorRemainsInExamination_WhenSecondAppointmentStillActive()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var staffRepository = new InMemoryStaffRepository();
            var service = new DoctorAppointmentService(dataSource, staffRepository);
            var first = MakeAppointment();
            var second = new Appointment { Id = 2, DoctorId = 10, Status = "Scheduled" };

            await service.BookAppointmentAsync(first);
            await service.BookAppointmentAsync(second);
            await service.FinishAppointmentAsync(first);

            Assert.Equal("IN_EXAMINATION", staffRepository.GetStatus(10));
        }


        [Fact]
        public async Task BookThenCancel_AppointmentStatusIsCanceled()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var staffRepository = new InMemoryStaffRepository();
            var service = new DoctorAppointmentService(dataSource, staffRepository);
            var appointment = MakeAppointment();

            await service.BookAppointmentAsync(appointment);
            await service.CancelAppointmentAsync(appointment);

            Assert.Equal("Canceled", dataSource.AllAppointments[0].Status);
        }

        [Fact]
        public async Task BookFinishThenCancel_ThrowsInvalidOperationException()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var staffRepository = new InMemoryStaffRepository();
            var service = new DoctorAppointmentService(dataSource, staffRepository);
            var appointment = MakeAppointment();

            await service.BookAppointmentAsync(appointment);
            await service.FinishAppointmentAsync(appointment);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CancelAppointmentAsync(appointment));
        }


        [Fact]
        public async Task AdminViewModel_AppointmentAppearsInList_AfterBookAndLoad()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var staffRepository = new InMemoryStaffRepository();
            var service = new DoctorAppointmentService(dataSource, staffRepository);
            var viewModel = new AdminAppointmentsViewModel(service);

            await viewModel.BookAppointmentAsync("PAT-1", 10, new DateTime(2025, 8, 1), new TimeSpan(9, 0, 0));
            await viewModel.LoadAppointmentsForDoctorAsync(10);

            Assert.Single(viewModel.AppointmentsList);
        }

        [Fact]
        public async Task AdminViewModel_AppointmentHasCanceledStatus_AfterCancelViaViewModelAndLoad()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var staffRepository = new InMemoryStaffRepository();
            var service = new DoctorAppointmentService(dataSource, staffRepository);
            var viewModel = new AdminAppointmentsViewModel(service);

            await service.BookAppointmentAsync(MakeAppointment());
            await viewModel.CancelAppointmentAsync(dataSource.AllAppointments[0]);
            await viewModel.LoadAppointmentsForDoctorAsync(10);

            Assert.Equal("Canceled", viewModel.AppointmentsList[0].Status);
        }

        [Fact]
        public async Task AdminViewModel_AppointmentHasFinishedStatus_AfterFinishViaViewModelAndLoad()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var staffRepository = new InMemoryStaffRepository();
            var service = new DoctorAppointmentService(dataSource, staffRepository);
            var viewModel = new AdminAppointmentsViewModel(service);

            await service.BookAppointmentAsync(MakeAppointment());
            await viewModel.FinishAppointmentAsync(dataSource.AllAppointments[0]);
            await viewModel.LoadAppointmentsForDoctorAsync(10);

            Assert.Equal("Finished", viewModel.AppointmentsList[0].Status);
        }


        [Fact]
        public async Task DoctorScheduleViewModel_ShowsAppointment_WhenItFallsOnSelectedDate()
        {
            var (viewModel, dataSource) = CreateScheduleStack();
            var selectedDate = new DateTime(2025, 8, 1);
            var appointment = new Appointment
            {
                Id = 1,
                DoctorId = 1,
                Date = selectedDate,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(9, 30, 0),
                Status = "Scheduled",
            };

            await dataSource.AddAppointmentAsync(
                0,
                appointment.DoctorId,
                appointment.Date.Date.Add(appointment.StartTime),
                appointment.Date.Date.Add(appointment.EndTime),
                appointment.Status);
            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = new DoctorScheduleViewModel.DoctorOption { DoctorId = 1, DoctorName = "Ana Pop" };

            await viewModel.LoadAsync();

            Assert.Single(viewModel.Appointments);
        }

        [Fact]
        public async Task DoctorScheduleViewModel_ShowsShift_WhenShiftAddedToRepository()
        {
            var shift = new Shift(
                1, TestPharmacist, "Ward A",
                new DateTime(2025, 8, 1, 8, 0, 0),
                new DateTime(2025, 8, 1, 16, 0, 0),
                ShiftStatus.SCHEDULED);
            var (viewModel, _) = CreateScheduleStack(new List<Shift> { shift });
            var selectedDate = new DateTime(2025, 8, 1);

            viewModel.SelectedDate = selectedDate;
            viewModel.SelectedDoctor = new DoctorScheduleViewModel.DoctorOption { DoctorId = 1, DoctorName = "Test" };

            await viewModel.LoadAsync();

            Assert.Single(viewModel.Shifts);
        }

        [Fact]
        public void VacationService_AddsShift_WhenExactlyAtFourDayLimit()
        {
            var staffRepository = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepository = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepository, shiftRepository);

            service.RegisterVacation(TestPharmacist.StaffID, new DateTime(2025, 7, 1), new DateTime(2025, 7, 4));

            Assert.Single(shiftRepository.GetAllShifts());
        }

        [Fact]
        public void VacationService_ShiftHasVacationStatus_WhenAtFourDayLimit()
        {
            var staffRepository = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepository = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepository, shiftRepository);

            service.RegisterVacation(TestPharmacist.StaffID, new DateTime(2025, 7, 1), new DateTime(2025, 7, 4));

            Assert.Equal(ShiftStatus.VACATION, shiftRepository.GetAllShifts()[0].Status);
        }

        [Fact]
        public void VacationService_ThrowsInvalidOperationException_WhenFifthDayExceedsMonthlyLimit()
        {
            var staffRepository = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepository = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepository, shiftRepository);
            service.RegisterVacation(TestPharmacist.StaffID, new DateTime(2025, 7, 1), new DateTime(2025, 7, 4));

            Assert.Throws<InvalidOperationException>(() =>
                service.RegisterVacation(TestPharmacist.StaffID, new DateTime(2025, 7, 10), new DateTime(2025, 7, 10)));
        }


        [Fact]
        public void PharmacistVacationViewModel_ReturnsSuccess_WhenExactlyAtFourDayLimit()
        {
            var staffRepository = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepository = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepository, shiftRepository);
            var viewModel = new PharmacistVacationViewModel(service);
            var choice = new PharmacistVacationViewModel.PharmacistChoice(TestPharmacist, "Ana Pop");

            var result = viewModel.TryRegisterVacation(
                choice,
                new DateTimeOffset(new DateTime(2025, 7, 1)),
                new DateTimeOffset(new DateTime(2025, 7, 4)));

            Assert.Equal(VacationRegistrationStatus.Success, result.status);
        }

        [Fact]
        public void PharmacistVacationViewModel_ReturnsError_WhenExceedingFourDayLimit()
        {
            var staffRepository = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepository = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepository, shiftRepository);
            var viewModel = new PharmacistVacationViewModel(service);
            var choice = new PharmacistVacationViewModel.PharmacistChoice(TestPharmacist, "Ana Pop");

            viewModel.TryRegisterVacation(
                choice,
                new DateTimeOffset(new DateTime(2025, 7, 1)),
                new DateTimeOffset(new DateTime(2025, 7, 4)));

            var result = viewModel.TryRegisterVacation(
                choice,
                new DateTimeOffset(new DateTime(2025, 7, 10)),
                new DateTimeOffset(new DateTime(2025, 7, 10)));

            Assert.Equal(VacationRegistrationStatus.Error, result.status);
        }
    }
}
