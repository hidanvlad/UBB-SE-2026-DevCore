using System;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using Xunit;

namespace DevCoreHospital.Tests
{
    public class ShiftRepositoryIntegrationTests
    {
        private const string InvalidConnectionString = "InvalidConnectionString";

        [Fact]
        public void AddShift_WhenShiftIsProvided_AddsShiftToCachedList()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var initialCount = shiftRepository.GetShifts().Count;
            var doctor = BuildDoctor(1, "Cardiology");
            var shift = BuildShift(101, doctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED);

            // Act
            shiftRepository.AddShift(shift);

            // Assert
            Assert.Equal(initialCount + 1, shiftRepository.GetShifts().Count);
            Assert.Equal(shift.Id, shiftRepository.GetShifts().Last().Id);
        }

        [Fact]
        public void UpdateShiftStatus_WhenShiftExists_ChangesShiftStatusInCache()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var doctor = BuildDoctor(2, "Neurology");
            var shiftId = 102;
            var shift = BuildShift(shiftId, doctor, "ER", DateTime.Today.AddHours(9), DateTime.Today.AddHours(13), ShiftStatus.SCHEDULED);
            shiftRepository.AddShift(shift);

            // Act
            shiftRepository.UpdateShiftStatus(shiftId, ShiftStatus.ACTIVE);

            // Assert
            var updatedShift = Assert.Single(shiftRepository.GetShifts().Where(existingShift => existingShift.Id == shiftId));
            Assert.Equal(ShiftStatus.ACTIVE, updatedShift.Status);
        }

        [Fact]
        public void CancelShift_WhenShiftExists_RemovesShiftFromCachedList()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var doctor = BuildDoctor(3, "Oncology");
            var shiftId = 103;
            var shift = BuildShift(shiftId, doctor, "ER", DateTime.Today.AddHours(10), DateTime.Today.AddHours(14), ShiftStatus.SCHEDULED);
            shiftRepository.AddShift(shift);

            // Act
            shiftRepository.CancelShift(shiftId);

            // Assert
            Assert.Equal(0, shiftRepository.GetShifts().Count(existingShift => existingShift.Id == shiftId));
        }

        [Fact]
        public void GetShiftsByStaffID_WhenShiftsHaveMixedStaff_ReturnsOnlyMatchingStaffShifts()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var doctorOne = BuildDoctor(4, "Cardiology");
            var doctorTwo = BuildDoctor(5, "Neurology");

            shiftRepository.AddShift(BuildShift(104, doctorOne, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(10), ShiftStatus.SCHEDULED));
            shiftRepository.AddShift(BuildShift(105, doctorTwo, "ER", DateTime.Today.AddHours(10), DateTime.Today.AddHours(12), ShiftStatus.SCHEDULED));
            shiftRepository.AddShift(BuildShift(106, doctorOne, "ER", DateTime.Today.AddHours(12), DateTime.Today.AddHours(14), ShiftStatus.SCHEDULED));

            // Act
            var result = shiftRepository.GetShiftsByStaffID(doctorOne.StaffID);

            // Assert
            Assert.Equal(new[] { 104, 106 }, result.Select(shift => shift.Id).OrderBy(id => id).ToArray());
        }

        [Fact]
        public void GetActiveShifts_WhenStatusesDiffer_ReturnsOnlyActiveShifts()
        {
            // Arrange
            var staffRepository = new StaffRepository(InvalidConnectionString);
            var shiftRepository = new ShiftRepository(InvalidConnectionString, staffRepository);
            var doctor = BuildDoctor(6, "Emergency Medicine");

            var activeShift = BuildShift(107, doctor, "ER", DateTime.Today.AddHours(8), DateTime.Today.AddHours(12), ShiftStatus.ACTIVE);
            var scheduledShift = BuildShift(108, doctor, "ER", DateTime.Today.AddHours(13), DateTime.Today.AddHours(17), ShiftStatus.SCHEDULED);
            shiftRepository.AddShift(activeShift);
            shiftRepository.AddShift(scheduledShift);

            // Act
            var activeShifts = shiftRepository.GetActiveShifts();

            // Assert
            Assert.Equal(new[] { 107 }, activeShifts.Select(shift => shift.Id).ToArray());
        }

        private static Doctor BuildDoctor(int staffId, string specialization)
            => new Doctor(staffId, "John", "Doe", "john.doe@example.com", string.Empty, false, specialization, "LIC-1", DoctorStatus.OFF_DUTY, 5);

        private static Shift BuildShift(int id, IStaff staff, string location, DateTime start, DateTime end, ShiftStatus status)
            => new Shift(id, staff, location, start, end, status);
    }
}
