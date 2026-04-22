using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;
using Xunit;

namespace DevCoreHospital.Tests
{
    public class ShiftManagementServiceTests
    {
        private readonly Mock<IShiftManagementStaffRepository> staffRepository;
        private readonly Mock<IShiftManagementShiftRepository> shiftRepository;
        private readonly ShiftManagementService service;

        public ShiftManagementServiceTests()
        {
            staffRepository = new Mock<IShiftManagementStaffRepository>();
            shiftRepository = new Mock<IShiftManagementShiftRepository>();
            service = new ShiftManagementService(staffRepository.Object, shiftRepository.Object);
        }

        [Fact]
        public void SetShiftActive_WhenShiftExists_UpdatesShiftStatusToActive()
        {
            // Arrange
            var shiftId = 100;
            var doctor = BuildDoctor(10, "Cardiology");
            shiftRepository
                .Setup(repo => repo.GetShifts())
                .Returns(new List<Shift>
                {
                    BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0))
                });

            int updateCount = 0;
            int updatedShiftId = -1;
            ShiftStatus updatedStatus = ShiftStatus.CANCELLED;
            shiftRepository
                .Setup(repo => repo.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback<int, ShiftStatus>((id, status) =>
                {
                    updateCount++;
                    updatedShiftId = id;
                    updatedStatus = status;
                });

            // Act
            service.SetShiftActive(shiftId);

            // Assert
            Assert.Equal(1, updateCount);
            Assert.Equal(shiftId, updatedShiftId);
            Assert.Equal(ShiftStatus.ACTIVE, updatedStatus);
        }

        [Fact]
        public void SetShiftActive_WhenShiftExists_UpdatesStaffAvailabilityToAvailable()
        {
            // Arrange
            var shiftId = 101;
            var doctor = BuildDoctor(11, "Neurology");
            shiftRepository
                .Setup(repo => repo.GetShifts())
                .Returns(new List<Shift>
                {
                    BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 9, 0, 0), new DateTime(2026, 4, 21, 17, 0, 0))
                });

            int updateCount = 0;
            int updatedStaffId = -1;
            bool updatedAvailability = false;
            DoctorStatus updatedDoctorStatus = DoctorStatus.IN_EXAMINATION;
            staffRepository
                .Setup(repo => repo.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback<int, bool, DoctorStatus>((staffId, isAvailable, status) =>
                {
                    updateCount++;
                    updatedStaffId = staffId;
                    updatedAvailability = isAvailable;
                    updatedDoctorStatus = status;
                });

            // Act
            service.SetShiftActive(shiftId);

            // Assert
            Assert.Equal(1, updateCount);
            Assert.Equal(doctor.StaffID, updatedStaffId);
            Assert.Equal(true, updatedAvailability);
            Assert.Equal(DoctorStatus.AVAILABLE, updatedDoctorStatus);
        }

        [Fact]
        public void SetShiftActive_WhenShiftDoesNotExist_DoesNotUpdateShiftStatus()
        {
            // Arrange
            shiftRepository.Setup(repo => repo.GetShifts()).Returns(new List<Shift>());

            int updateCount = 0;
            shiftRepository
                .Setup(repo => repo.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback(() => updateCount++);

            // Act
            service.SetShiftActive(999);

            // Assert
            Assert.Equal(0, updateCount);
        }

        [Fact]
        public void SetShiftActive_WhenShiftDoesNotExist_DoesNotUpdateStaffAvailability()
        {
            // Arrange
            shiftRepository.Setup(repo => repo.GetShifts()).Returns(new List<Shift>());

            int updateCount = 0;
            staffRepository
                .Setup(repo => repo.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback(() => updateCount++);

            // Act
            service.SetShiftActive(999);

            // Assert
            Assert.Equal(0, updateCount);
        }

        [Fact]
        public void CancelShift_WhenShiftExists_UpdatesStaffAvailabilityToOffDuty()
        {
            // Arrange
            var shiftId = 200;
            var doctor = BuildDoctor(20, "Oncology");
            shiftRepository
                .Setup(repo => repo.GetShifts())
                .Returns(new List<Shift>
                {
                    BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0))
                });

            int updateCount = 0;
            int updatedStaffId = -1;
            bool updatedAvailability = true;
            DoctorStatus updatedDoctorStatus = DoctorStatus.AVAILABLE;
            staffRepository
                .Setup(repo => repo.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback<int, bool, DoctorStatus>((staffId, isAvailable, status) =>
                {
                    updateCount++;
                    updatedStaffId = staffId;
                    updatedAvailability = isAvailable;
                    updatedDoctorStatus = status;
                });

            // Act
            service.CancelShift(shiftId);

            // Assert
            Assert.Equal(1, updateCount);
            Assert.Equal(doctor.StaffID, updatedStaffId);
            Assert.Equal(false, updatedAvailability);
            Assert.Equal(DoctorStatus.OFF_DUTY, updatedDoctorStatus);
        }

        [Fact]
        public void CancelShift_WhenShiftExists_UpdatesShiftStatusToCompleted()
        {
            // Arrange
            var shiftId = 201;
            var doctor = BuildDoctor(21, "Cardiology");
            shiftRepository
                .Setup(repo => repo.GetShifts())
                .Returns(new List<Shift>
                {
                    BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 10, 0, 0), new DateTime(2026, 4, 21, 18, 0, 0))
                });

            int updateCount = 0;
            int updatedShiftId = -1;
            ShiftStatus updatedStatus = ShiftStatus.SCHEDULED;
            shiftRepository
                .Setup(repo => repo.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback<int, ShiftStatus>((id, status) =>
                {
                    updateCount++;
                    updatedShiftId = id;
                    updatedStatus = status;
                });

            // Act
            service.CancelShift(shiftId);

            // Assert
            Assert.Equal(1, updateCount);
            Assert.Equal(shiftId, updatedShiftId);
            Assert.Equal(ShiftStatus.COMPLETED, updatedStatus);
        }

        [Fact]
        public void CancelShift_WhenShiftDoesNotExist_DoesNotUpdateStaffAvailability()
        {
            // Arrange
            shiftRepository.Setup(repo => repo.GetShifts()).Returns(new List<Shift>());

            int updateCount = 0;
            staffRepository
                .Setup(repo => repo.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback(() => updateCount++);

            // Act
            service.CancelShift(999);

            // Assert
            Assert.Equal(0, updateCount);
        }

        [Fact]
        public void CancelShift_WhenShiftDoesNotExist_DoesNotUpdateShiftStatus()
        {
            // Arrange
            shiftRepository.Setup(repo => repo.GetShifts()).Returns(new List<Shift>());

            int updateCount = 0;
            shiftRepository
                .Setup(repo => repo.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback(() => updateCount++);

            // Act
            service.CancelShift(999);

            // Assert
            Assert.Equal(0, updateCount);
        }

        [Theory]
        [InlineData(8, 10, true)]
        [InlineData(9, 11, false)]
        [InlineData(10, 12, false)]
        [InlineData(11, 13, false)]
        [InlineData(12, 14, true)]
        public void ValidateNoOverlap_WhenBoundaryAndOverlapCasesAreChecked_ReturnsExpectedResult(
            int candidateStartHour,
            int candidateEndHour,
            bool expected)
        {
            // Arrange
            var day = new DateTime(2026, 4, 21);
            var existingDoctor = BuildDoctor(25, "Cardiology");
            var existingShift = BuildShift(1, existingDoctor, day.AddHours(10), day.AddHours(12));
            shiftRepository.Setup(repo => repo.GetShifts()).Returns(new List<Shift> { existingShift });

            // Act
            var result = service.ValidateNoOverlap(existingDoctor.StaffID, day.AddHours(candidateStartHour), day.AddHours(candidateEndHour));

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ValidateNoOverlap_WhenShiftsBelongToOtherStaff_ReturnsTrue()
        {
            // Arrange
            var day = new DateTime(2026, 4, 21);
            var otherDoctor = BuildDoctor(200, "Neurology");
            var shiftForOtherStaff = BuildShift(10, otherDoctor, day.AddHours(10), day.AddHours(12));
            shiftRepository.Setup(repo => repo.GetShifts()).Returns(new List<Shift> { shiftForOtherStaff });

            // Act
            var result = service.ValidateNoOverlap(201, day.AddHours(10), day.AddHours(12));

            // Assert
            Assert.Equal(true, result);
        }

        [Theory]
        [InlineData("Pharmacy")]
        [InlineData("pharmacy")]
        [InlineData("PHARMACY")]
        public void GetFilteredStaff_WhenLocationIsPharmacy_ReturnsMatchingPharmacistStaffIds(string location)
        {
            // Arrange
            var matchingPharmacist = BuildPharmacyst(1, "Sterile Compounding");
            var nonMatchingPharmacist = BuildPharmacyst(2, "Oncology");
            var doctor = BuildDoctor(3, "Cardiology");
            staffRepository.Setup(repo => repo.LoadAllStaff()).Returns(new List<IStaff>
            {
                matchingPharmacist,
                nonMatchingPharmacist,
                doctor,
            });

            // Act
            var result = service.GetFilteredStaff(location, "sterile");

            // Assert
            Assert.Equal(new[] { matchingPharmacist.StaffID }, result.Select(staff => staff.StaffID).ToArray());
        }

        [Fact]
        public void GetFilteredStaff_WhenLocationIsNotPharmacy_ReturnsMatchingDoctorStaffIds()
        {
            // Arrange
            var matchingDoctor = BuildDoctor(10, "Cardiology");
            var nonMatchingDoctor = BuildDoctor(11, "Neurology");
            var pharmacist = BuildPharmacyst(12, "Sterile Compounding");
            staffRepository.Setup(repo => repo.LoadAllStaff()).Returns(new List<IStaff>
            {
                matchingDoctor,
                nonMatchingDoctor,
                pharmacist,
            });

            // Act
            var result = service.GetFilteredStaff("ER", "cardio");

            // Assert
            Assert.Equal(new[] { matchingDoctor.StaffID }, result.Select(staff => staff.StaffID).ToArray());
        }

        [Fact]
        public void FindStaffReplacements_WhenShiftIsNull_ReturnsEmptyList()
        {
            // Act
            var result = service.FindStaffReplacements(null!);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void FindStaffReplacements_WhenAppointedStaffIsNull_ReturnsEmptyList()
        {
            // Arrange
            var shift = BuildShift(90, BuildDoctor(900, "Cardiology"), new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));
            shift.AppointedStaff = null!;

            // Act
            var result = service.FindStaffReplacements(shift);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void FindStaffReplacements_WhenCandidatesIncludeOverlapAndDifferentType_ReturnsOnlyCompatibleNonOverlappingStaff()
        {
            // Arrange
            var day = new DateTime(2026, 4, 21);
            var currentDoctor = BuildDoctor(1, "Cardiology");
            var candidateNoOverlap = BuildDoctor(2, "Cardiology");
            var candidateOverlap = BuildDoctor(3, "Cardiology");
            var candidateDifferentType = BuildPharmacyst(4, "Sterile Compounding");
            var anotherNoOverlapDoctor = BuildDoctor(5, "Cardiology");

            var targetShift = BuildShift(1000, currentDoctor, day.AddHours(10), day.AddHours(12));
            var conflictingShift = BuildShift(2000, candidateOverlap, day.AddHours(11), day.AddHours(13));

            staffRepository.Setup(repo => repo.LoadAllStaff()).Returns(new List<IStaff>
            {
                currentDoctor,
                candidateNoOverlap,
                candidateOverlap,
                candidateDifferentType,
                anotherNoOverlapDoctor,
            });

            shiftRepository.Setup(repo => repo.GetShifts()).Returns(new List<Shift>
            {
                targetShift,
                conflictingShift,
            });

            // Act
            var replacements = service.FindStaffReplacements(targetShift);

            // Assert
            Assert.Equal(
                new[]
                {
                    candidateNoOverlap.StaffID,
                    anotherNoOverlapDoctor.StaffID,
                },
                replacements.Select(staff => staff.StaffID).ToArray());
        }

        [Fact]
        public void SetShiftActive_WhenExecutingActivationFlow_InvokesExpectedRepositoryCallOrder()
        {
            // Arrange
            var shiftId = 500;
            var doctor = BuildDoctor(50, "Emergency Medicine");
            var shift = BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));
            var callOrder = new List<string>();

            shiftRepository
                .Setup(repo => repo.GetShifts())
                .Returns(() =>
                {
                    callOrder.Add("GetShifts");
                    return new List<Shift> { shift };
                });

            shiftRepository
                .Setup(repo => repo.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback(() => callOrder.Add("UpdateShiftStatus"));

            staffRepository
                .Setup(repo => repo.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback(() => callOrder.Add("UpdateStaffAvailability"));

            // Act
            service.SetShiftActive(shiftId);

            // Assert
            Assert.Equal(
                new[]
                {
                    "GetShifts",
                    "UpdateShiftStatus",
                    "UpdateStaffAvailability",
                },
                callOrder);
        }

        [Fact]
        public void CancelShift_WhenExecutingCancellationFlow_InvokesExpectedRepositoryCallOrder()
        {
            // Arrange
            var shiftId = 501;
            var doctor = BuildDoctor(51, "Emergency Medicine");
            var shift = BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));
            var callOrder = new List<string>();

            shiftRepository
                .Setup(repo => repo.GetShifts())
                .Returns(() =>
                {
                    callOrder.Add("GetShifts");
                    return new List<Shift> { shift };
                });

            staffRepository
                .Setup(repo => repo.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback(() => callOrder.Add("UpdateStaffAvailability"));

            shiftRepository
                .Setup(repo => repo.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback(() => callOrder.Add("UpdateShiftStatus"));

            // Act
            service.CancelShift(shiftId);

            // Assert
            Assert.Equal(
                new[]
                {
                    "GetShifts",
                    "UpdateStaffAvailability",
                    "UpdateShiftStatus",
                },
                callOrder);
        }

        private static Shift BuildShift(int id, IStaff appointedStaff, DateTime start, DateTime end, ShiftStatus status = ShiftStatus.SCHEDULED)
            => new Shift(id, appointedStaff, "ER", start, end, status);

        private static Doctor BuildDoctor(int staffId, string specialization)
            => new Doctor(staffId, "John", "Doe", "john.doe@example.com", string.Empty, false, specialization, "LIC-1", DoctorStatus.OFF_DUTY, 5);

        private static Pharmacyst BuildPharmacyst(int staffId, string certification)
            => new Pharmacyst(staffId, "Pharma", "Cist", "pharma@example.com", true, certification, 4);
    }
}
