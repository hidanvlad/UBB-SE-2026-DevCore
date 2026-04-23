using System;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels.Admin;
using Xunit;

namespace DevCoreHospital.Tests
{
    public class AdminShiftViewModelIntegrationTests
    {
        private const string InvalidConnectionString = "InvalidConnectionString";

        [Fact]
        public void CreateNewShift_WhenNoOverlap_AddsShiftToRepositoryAndViewModel()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);
            var viewModel = new AdminShiftViewModel(service);

            var doctor = BuildDoctor(21, "Cardiology");
            var start = DateTime.Today.AddHours(8);
            var end = DateTime.Today.AddHours(12);
            var initialCount = shiftRepository.GetShifts().Count;

            // Act
            viewModel.CreateNewShift(doctor, start, end, "ER");

            // Assert
            Assert.Equal(initialCount + 1, shiftRepository.GetShifts().Count);

            var repositoryShift = shiftRepository.GetShifts().Last();
            Assert.Equal(doctor.StaffID, repositoryShift.AppointedStaff.StaffID);
            Assert.Equal("ER", repositoryShift.Location);
            Assert.Equal(ShiftStatus.SCHEDULED, repositoryShift.Status);

            var viewModelShift = Assert.Single(viewModel.Shifts.Where(shift =>
                shift.StartTime == start &&
                shift.EndTime == end &&
                shift.Location == "ER"));
            Assert.Equal(ShiftStatus.SCHEDULED, viewModelShift.Status);
        }

        [Fact]
        public void SetShiftActive_WhenShiftExists_UpdatesStatusInRepositoryAndViewModel()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            var doctor = BuildDoctor(22, "Neurology");
            var shiftId = 301;
            shiftRepository.AddShift(BuildShift(shiftId, doctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(16), ShiftStatus.SCHEDULED));

            var viewModel = new AdminShiftViewModel(service);

            // Act
            viewModel.SetShiftActive(shiftId);

            // Assert
            var repositoryShift = Assert.Single(shiftRepository.GetShifts().Where(shift => shift.Id == shiftId));
            Assert.Equal(ShiftStatus.ACTIVE, repositoryShift.Status);

            var viewModelShift = Assert.Single(viewModel.Shifts.Where(shift => shift.Id == shiftId));
            Assert.Equal(ShiftStatus.ACTIVE, viewModelShift.Status);
        }

        [Fact]
        public void CancelShift_WhenShiftExists_UpdatesStatusInRepositoryAndViewModel()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            var doctor = BuildDoctor(23, "Oncology");
            var shiftId = 302;
            shiftRepository.AddShift(BuildShift(shiftId, doctor, "ER", DateTime.Today.AddHours(9), DateTime.Today.AddHours(17), ShiftStatus.SCHEDULED));

            var viewModel = new AdminShiftViewModel(service);

            // Act
            viewModel.CancelShift(shiftId);

            // Assert
            var repositoryShift = Assert.Single(shiftRepository.GetShifts().Where(shift => shift.Id == shiftId));
            Assert.Equal(ShiftStatus.COMPLETED, repositoryShift.Status);

            var viewModelShift = Assert.Single(viewModel.Shifts.Where(shift => shift.Id == shiftId));
            Assert.Equal(ShiftStatus.COMPLETED, viewModelShift.Status);
        }

        [Fact]
        public void SelectedDepartment_WhenSet_FiltersShiftsInViewModel()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            var doctor = BuildDoctor(24, "Cardiology");
            var erShift = BuildShift(401, doctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(10), ShiftStatus.SCHEDULED);
            var pharmacyShift = BuildShift(402, doctor, "Pharmacy", DateTime.Today.AddHours(11), DateTime.Today.AddHours(13), ShiftStatus.SCHEDULED);
            shiftRepository.AddShift(erShift);
            shiftRepository.AddShift(pharmacyShift);

            var viewModel = new AdminShiftViewModel(service);

            // Act
            viewModel.IsWeeklyView = true;
            viewModel.SelectedDepartment = "ER";

            // Assert
            Assert.Equal(1, viewModel.Shifts.Count);
            Assert.Equal("ER", viewModel.Shifts[0].Location);
            Assert.Equal(erShift.Id, viewModel.Shifts[0].Id);
        }

        private static Doctor BuildDoctor(int staffId, string specialization)
            => new Doctor(staffId, "John", "Doe", "john.doe@example.com", string.Empty, false, specialization, "LIC-1", DoctorStatus.OFF_DUTY, 5);

        private static Shift BuildShift(int id, IStaff staff, string location, DateTime start, DateTime end, ShiftStatus status)
            => new Shift(id, staff, location, start, end, status);
    }
}
