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
using Moq;

namespace DevCoreHospital.Tests.Integration
{
    public class AppointmentFlowIntegrationTests
    {
        private sealed class InMemoryAppointmentDataSource : IAppointmentRepository
        {
            private readonly List<Appointment> appointments = new();
            private readonly Dictionary<int, string> doctorStatuses = new();
            private readonly List<(int DoctorId, string DoctorName)> doctors;

            public InMemoryAppointmentDataSource(List<(int DoctorId, string DoctorName)>? doctors = null)
                => this.doctors = doctors ?? new List<(int DoctorId, string DoctorName)>();

            public IReadOnlyList<Appointment> AllAppointments => appointments.AsReadOnly();

            public string GetDoctorStatus(int doctorId) =>
                doctorStatuses.TryGetValue(doctorId, out var status) ? status : string.Empty;

            public Task AddAppointmentAsync(Appointment appointment)
            {
                appointments.Add(appointment);
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

            public Task<int> GetActiveAppointmentsCountForDoctorAsync(int doctorId)
            {
                bool IsActiveAppointmentForDoctor(Appointment appointment) =>
                    appointment.DoctorId == doctorId &&
                    !string.Equals(appointment.Status, "Finished", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(appointment.Status, "Canceled", StringComparison.OrdinalIgnoreCase);
                int count = appointments.Count(IsActiveAppointmentForDoctor);
                return Task.FromResult(count);
            }

            public Task UpdateDoctorStatusAsync(int doctorId, string status)
            {
                doctorStatuses[doctorId] = status;
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<Appointment>> GetUpcomingAppointmentsAsync(
                int doctorUserId, DateTime fromDate, int skip, int take)
            {
                bool IsUpcomingForDoctor(Appointment appointment) =>
                    appointment.DoctorId == doctorUserId && appointment.Date >= fromDate;
                IReadOnlyList<Appointment> result = appointments
                    .Where(IsUpcomingForDoctor)
                    .Skip(skip).Take(take)
                    .ToList();
                return Task.FromResult(result);
            }

            public Task<IReadOnlyList<(int DoctorId, string DoctorName)>> GetAllDoctorsAsync()
            {
                IReadOnlyList<(int DoctorId, string DoctorName)> result = doctors;
                return Task.FromResult(result);
            }

            public Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
            {
                bool HasMatchingId(Appointment appointment) => appointment.Id == appointmentId;
                Appointment? result = appointments.FirstOrDefault(HasMatchingId);
                return Task.FromResult(result);
            }

            public Task<IReadOnlyList<Appointment>> GetAppointmentsForAdminAsync(int doctorId)
            {
                bool IsForDoctor(Appointment appointment) => appointment.DoctorId == doctorId;
                IReadOnlyList<Appointment> result = appointments
                    .Where(IsForDoctor)
                    .ToList();
                return Task.FromResult(result);
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

            public List<Shift> GetShifts() => shifts;

            public List<Shift> GetShiftsByStaffID(int staffId)
            {
                bool IsForStaff(Shift shift) => shift.AppointedStaff.StaffID == staffId;
                return shifts.Where(IsForStaff).ToList();
            }

            public void AddShift(Shift shift) => shifts.Add(shift);
        }

        private sealed class InMemoryShiftRepository : IShiftRepository
        {
            private readonly List<Shift> shifts;

            public InMemoryShiftRepository(List<Shift> shifts) => this.shifts = shifts;

            public IReadOnlyList<Shift> GetShiftsForStaffInRange(int staffId, DateTime from, DateTime to)
            {
                bool IsInRangeForStaff(Shift shift) =>
                    shift.AppointedStaff.StaffID == staffId
                    && shift.StartTime < to
                    && shift.EndTime > from;
                return shifts.Where(IsInRangeForStaff).ToList();
            }

            public Shift? GetShiftById(int shiftId)
            {
                bool HasMatchingId(Shift shift) => shift.Id == shiftId;
                return shifts.FirstOrDefault(HasMatchingId);
            }

            public List<Shift> GetShiftsByStaffID(int staffId)
            {
                bool IsForStaff(Shift shift) => shift.AppointedStaff.StaffID == staffId;
                return shifts.Where(IsForStaff).ToList();
            }
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
            var shiftRepo = new InMemoryShiftRepository(shifts ?? new List<Shift>());
            var mockUser = new Mock<ICurrentUserService>();
            mockUser.Setup(u => u.Role).Returns("Doctor");
            mockUser.Setup(u => u.UserId).Returns(1);
            var service = new DoctorAppointmentService(dataSource, shiftRepo);
            var viewModel = new DoctorScheduleViewModel(
                mockUser.Object,
                service,
                new Mock<IDialogService>().Object);
            return (viewModel, dataSource);
        }


        [Fact]
        public async Task BookThenFinish_AppointmentStatusIsFinished()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var service = new DoctorAppointmentService(dataSource);
            var appointment = MakeAppointment();

            await service.BookAppointmentAsync(appointment);
            await service.FinishAppointmentAsync(appointment);

            Assert.Equal("Finished", dataSource.AllAppointments[0].Status);
        }

        [Fact]
        public async Task BookThenFinish_DoctorStatusIsAvailable_WhenLastActiveAppointmentDone()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var service = new DoctorAppointmentService(dataSource);
            var appointment = MakeAppointment();

            await service.BookAppointmentAsync(appointment);
            await service.FinishAppointmentAsync(appointment);

            Assert.Equal("AVAILABLE", dataSource.GetDoctorStatus(10));
        }

        [Fact]
        public async Task BookThenFinish_DoctorRemainsInExamination_WhenSecondAppointmentStillActive()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var service = new DoctorAppointmentService(dataSource);
            var first = MakeAppointment();
            var second = new Appointment { Id = 2, DoctorId = 10, Status = "Scheduled" };

            await service.BookAppointmentAsync(first);
            await service.BookAppointmentAsync(second);
            await service.FinishAppointmentAsync(first);

            Assert.Equal("IN_EXAMINATION", dataSource.GetDoctorStatus(10));
        }


        [Fact]
        public async Task BookThenCancel_AppointmentStatusIsCanceled()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var service = new DoctorAppointmentService(dataSource);
            var appointment = MakeAppointment();

            await service.BookAppointmentAsync(appointment);
            await service.CancelAppointmentAsync(appointment);

            Assert.Equal("Canceled", dataSource.AllAppointments[0].Status);
        }

        [Fact]
        public async Task BookFinishThenCancel_ThrowsInvalidOperationException()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var service = new DoctorAppointmentService(dataSource);
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
            var service = new DoctorAppointmentService(dataSource);
            var viewModel = new AdminAppointmentsViewModel(service);

            await viewModel.BookAppointmentAsync("PAT-1", 10, new DateTime(2025, 8, 1), new TimeSpan(9, 0, 0));
            await viewModel.LoadAppointmentsForDoctorAsync(10);

            Assert.Single(viewModel.AppointmentsList);
        }

        [Fact]
        public async Task AdminViewModel_AppointmentHasCanceledStatus_AfterCancelViaViewModelAndLoad()
        {
            var dataSource = new InMemoryAppointmentDataSource();
            var service = new DoctorAppointmentService(dataSource);
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
            var service = new DoctorAppointmentService(dataSource);
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

            await dataSource.AddAppointmentAsync(appointment);
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
            var staffRepo = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepo = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepo, shiftRepo);

            service.RegisterVacation(TestPharmacist.StaffID, new DateTime(2025, 7, 1), new DateTime(2025, 7, 4));

            Assert.Single(shiftRepo.GetShifts());
        }

        [Fact]
        public void VacationService_ShiftHasVacationStatus_WhenAtFourDayLimit()
        {
            var staffRepo = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepo = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepo, shiftRepo);

            service.RegisterVacation(TestPharmacist.StaffID, new DateTime(2025, 7, 1), new DateTime(2025, 7, 4));

            Assert.Equal(ShiftStatus.VACATION, shiftRepo.GetShifts()[0].Status);
        }

        [Fact]
        public void VacationService_ThrowsInvalidOperationException_WhenFifthDayExceedsMonthlyLimit()
        {
            var staffRepo = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepo = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepo, shiftRepo);
            service.RegisterVacation(TestPharmacist.StaffID, new DateTime(2025, 7, 1), new DateTime(2025, 7, 4));

            Assert.Throws<InvalidOperationException>(() =>
                service.RegisterVacation(TestPharmacist.StaffID, new DateTime(2025, 7, 10), new DateTime(2025, 7, 10)));
        }


        [Fact]
        public void PharmacistVacationViewModel_ReturnsSuccess_WhenExactlyAtFourDayLimit()
        {
            var staffRepo = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepo = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepo, shiftRepo);
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
            var staffRepo = new InMemoryPharmacyStaffRepository(new List<Pharmacyst> { TestPharmacist });
            var shiftRepo = new InMemoryPharmacyShiftRepository();
            var service = new PharmacyVacationService(staffRepo, shiftRepo);
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
