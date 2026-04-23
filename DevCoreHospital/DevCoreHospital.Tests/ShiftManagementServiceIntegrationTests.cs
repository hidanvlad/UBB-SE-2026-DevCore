using System;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Xunit;

namespace DevCoreHospital.Tests
{
    public class ShiftManagementServiceIntegrationTests
    {
        private const string InvalidConnectionString = "InvalidConnectionString";

        [Fact]
        public void AddShift_WhenShiftIsProvided_AddsShiftToRepository()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            var initialCount = shiftRepository.GetShifts().Count;
            var doctor = BuildDoctor(11, "Cardiology");
            var shift = BuildShift(201, doctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED);

            // Act
            service.AddShift(shift);

            // Assert
            Assert.Equal(initialCount + 1, shiftRepository.GetShifts().Count);
        }

        [Fact]
        public void ValidateNoOverlap_WhenShiftOverlapsExistingShift_ReturnsFalse()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            var doctor = BuildDoctor(12, "Neurology");
            shiftRepository.AddShift(BuildShift(202, doctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED));

            // Act
            var result = service.ValidateNoOverlap(doctor.StaffID, DateTime.Today.AddHours(10), DateTime.Today.AddHours(14));

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateNoOverlap_WhenShiftDoesNotOverlapExistingShift_ReturnsTrue()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            var doctor = BuildDoctor(13, "Oncology");
            shiftRepository.AddShift(BuildShift(203, doctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED));

            // Act
            var result = service.ValidateNoOverlap(doctor.StaffID, DateTime.Today.AddHours(12), DateTime.Today.AddHours(16));

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void SetShiftActive_WhenShiftExists_UpdatesStatusToActiveInRepository()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            var doctor = BuildDoctor(14, "Cardiology");
            var shiftId = 204;
            shiftRepository.AddShift(BuildShift(shiftId, doctor, "ER", DateTime.Today.AddHours(9), DateTime.Today.AddHours(17), ShiftStatus.SCHEDULED));

            // Act
            service.SetShiftActive(shiftId);

            // Assert
            var shift = Assert.Single(shiftRepository.GetShifts().Where(existingShift => existingShift.Id == shiftId));
            Assert.Equal(ShiftStatus.ACTIVE, shift.Status);
        }

        [Fact]
        public void CancelShift_WhenShiftExists_UpdatesStatusToCompletedInRepository()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            var doctor = BuildDoctor(15, "Emergency Medicine");
            var shiftId = 205;
            shiftRepository.AddShift(BuildShift(shiftId, doctor, "ER", DateTime.Today.AddHours(7), DateTime.Today.AddHours(15), ShiftStatus.SCHEDULED));

            // Act
            service.CancelShift(shiftId);

            // Assert
            var shift = Assert.Single(shiftRepository.GetShifts().Where(existingShift => existingShift.Id == shiftId));
            Assert.Equal(ShiftStatus.COMPLETED, shift.Status);
        }

        [Fact]
        public void ReassignShift_WhenInputsAreValid_ChangesAppointedStaffAndReturnsTrue()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            var originalDoctor = BuildDoctor(16, "Cardiology");
            var replacementDoctor = BuildDoctor(17, "Cardiology");
            var shift = BuildShift(206, originalDoctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED);

            // Act
            var result = service.ReassignShift(shift, replacementDoctor);

            // Assert
            Assert.True(result);
            Assert.Equal(replacementDoctor.StaffID, shift.AppointedStaff.StaffID);
        }

        private static Doctor BuildDoctor(int staffId, string specialization)
            => new Doctor(staffId, "John", "Doe", "john.doe@example.com", string.Empty, false, specialization, "LIC-1", DoctorStatus.OFF_DUTY, 5);

        private static Shift BuildShift(int id, IStaff staff, string location, DateTime start, DateTime end, ShiftStatus status)
            => new Shift(id, staff, location, start, end, status);
    }
}
