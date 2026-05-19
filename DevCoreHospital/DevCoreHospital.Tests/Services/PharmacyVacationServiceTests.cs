using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class PharmacyVacationServiceTests
    {
        private readonly Mock<IPharmacyStaffRepository> mockStaffRepository;
        private readonly Mock<IPharmacyShiftRepository> mockShiftRepository;
        private readonly PharmacyVacationService service;

        private readonly Pharmacyst pharmacist = new Pharmacyst(1, "Ana", "Pop", string.Empty, true, "General", 3);

        public PharmacyVacationServiceTests()
        {
            mockStaffRepository = new Mock<IPharmacyStaffRepository>();
            mockShiftRepository = new Mock<IPharmacyShiftRepository>();
            service = new PharmacyVacationService(mockStaffRepository.Object, mockShiftRepository.Object);
        }


        [Fact]
        public void RegisterVacation_ThrowsArgumentException_WhenEndDateIsBeforeStartDate()
        {
            var startDate = new DateTime(2025, 6, 10);
            var endDate = new DateTime(2025, 6, 5);

            var exception = Assert.Throws<ArgumentException>(() =>
                service.RegisterVacation(pharmacist.StaffID, startDate, endDate));

            Assert.Equal("End date must be on or after start date.", exception.Message);
        }

        [Fact]
        public void RegisterVacation_ThrowsArgumentException_WhenPharmacistNotFound()
        {
            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst>());

            var exception = Assert.Throws<ArgumentException>(() =>
                service.RegisterVacation(99, new DateTime(2025, 6, 1), new DateTime(2025, 6, 3)));

            Assert.Equal("Pharmacist not found.", exception.Message);
        }

        [Fact]
        public void RegisterVacation_ThrowsInvalidOperationException_WhenVacationOverlapsExistingShift()
        {
            var existingShift = new Shift(10, pharmacist, "Ward A",
                new DateTime(2025, 6, 8), new DateTime(2025, 6, 12), ShiftStatus.SCHEDULED);

            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift> { existingShift });

            var exception = Assert.Throws<InvalidOperationException>(() =>
                service.RegisterVacation(pharmacist.StaffID, new DateTime(2025, 6, 10), new DateTime(2025, 6, 15)));

            Assert.Equal("Cannot add vacation: this period overlaps an existing shift.", exception.Message);
        }

        [Fact]
        public void RegisterVacation_ThrowsInvalidOperationException_WhenVacationWouldExceedMonthlyLimit()
        {
            var existingVacation = new Shift(10, pharmacist, "Vacation",
                new DateTime(2025, 6, 1), new DateTime(2025, 6, 4), ShiftStatus.VACATION);

            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift> { existingVacation });

            var exception = Assert.Throws<InvalidOperationException>(() =>
                service.RegisterVacation(pharmacist.StaffID, new DateTime(2025, 6, 20), new DateTime(2025, 6, 21)));

            Assert.Equal("Cannot add vacation: pharmacist would exceed 4 vacation days in a month.", exception.Message);
        }

        [Fact]
        public void RegisterVacation_AddsVacationShift_WhenAllConditionsAreMet()
        {
            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());

            service.RegisterVacation(pharmacist.StaffID, new DateTime(2025, 7, 1), new DateTime(2025, 7, 3));

            mockShiftRepository.Verify(pharmacyShiftRepository => pharmacyShiftRepository.AddShift(It.IsAny<Shift>()), Times.Once);
        }

        [Fact]
        public void RegisterVacation_AddsShiftWithVacationStatus_WhenAllConditionsAreMet()
        {
            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());

            service.RegisterVacation(pharmacist.StaffID, new DateTime(2025, 7, 1), new DateTime(2025, 7, 3));

            mockShiftRepository.Verify(pharmacyShiftRepository => pharmacyShiftRepository.AddShift(
                It.Is<Shift>(shift => shift.Status == ShiftStatus.VACATION)), Times.Once);
        }

        [Fact]
        public void RegisterVacation_AllowsVacation_WhenExactlyAtMonthlyLimit()
        {
            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());

            service.RegisterVacation(pharmacist.StaffID, new DateTime(2025, 6, 1), new DateTime(2025, 6, 4));

            mockShiftRepository.Verify(pharmacyShiftRepository => pharmacyShiftRepository.AddShift(It.IsAny<Shift>()), Times.Once);
        }

        [Fact]
        public void RegisterVacation_ThrowsInvalidOperationException_WhenNewVacationExceedsLimitAcrossMonths()
        {
            var existingVacation = new Shift(10, pharmacist, "Vacation",
                new DateTime(2025, 7, 1), new DateTime(2025, 7, 4), ShiftStatus.VACATION);

            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift> { existingVacation });

            Assert.Throws<InvalidOperationException>(() =>
                service.RegisterVacation(pharmacist.StaffID, new DateTime(2025, 7, 28), new DateTime(2025, 7, 31)));
        }

        [Fact]
        public void RegisterVacation_Succeeds_WhenStartDateEqualsEndDate()
        {
            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());

            service.RegisterVacation(pharmacist.StaffID, new DateTime(2025, 7, 10), new DateTime(2025, 7, 10));

            mockShiftRepository.Verify(pharmacyShiftRepository => pharmacyShiftRepository.AddShift(It.IsAny<Shift>()), Times.Once);
        }

        [Fact]
        public void RegisterVacation_Succeeds_WhenVacationSpansTwoMonthsButNeitherExceedsLimit()
        {
            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift>());

            service.RegisterVacation(pharmacist.StaffID, new DateTime(2025, 6, 30), new DateTime(2025, 7, 2));

            mockShiftRepository.Verify(pharmacyShiftRepository => pharmacyShiftRepository.AddShift(It.IsAny<Shift>()), Times.Once);
        }

        [Fact]
        public void GetPharmacists_WhenUnordered_ReturnsThemSortedByFirstNameThenLastName()
        {
            var pharmacists = new List<Pharmacyst>
            {
                new Pharmacyst(3, "Zoe", "Adams", string.Empty, true, "Sterile", 2),
                new Pharmacyst(1, "Ana", "Brown", string.Empty, true, "General", 1),
                new Pharmacyst(2, "Ana", "Adams", string.Empty, true, "IV", 3),
            };
            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(pharmacists);

            string ToFullName(Pharmacyst pharmacist) => $"{pharmacist.FirstName} {pharmacist.LastName}";
            var orderedNames = service.GetPharmacists()
                .Select(ToFullName)
                .ToArray();

            Assert.Equal(new[] { "Ana Adams", "Ana Brown", "Zoe Adams" }, orderedNames);
        }

        [Fact]
        public void RegisterVacation_ThrowsInvalidOperationException_WhenNewVacationExceedsLimitInSecondMonth()
        {
            var existingVacation = new Shift(10, pharmacist, "Vacation",
                new DateTime(2025, 7, 1), new DateTime(2025, 7, 4), ShiftStatus.VACATION);

            mockStaffRepository.Setup(pharmacyStaffRepository => pharmacyStaffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });
            mockShiftRepository.Setup(pharmacyShiftRepository => pharmacyShiftRepository.GetAllShifts()).Returns(new List<Shift> { existingVacation });

            Assert.Throws<InvalidOperationException>(() =>
                service.RegisterVacation(pharmacist.StaffID, new DateTime(2025, 6, 30), new DateTime(2025, 7, 3)));
        }
    }
}
