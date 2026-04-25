using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class PharmacyScheduleServiceTests
    {
        private readonly Mock<IShiftRepository> shiftRepositoryMock;
        private readonly Mock<IPharmacyStaffRepository> staffRepositoryMock;
        private readonly PharmacyScheduleService service;

        public PharmacyScheduleServiceTests()
        {
            shiftRepositoryMock = new Mock<IShiftRepository>();
            staffRepositoryMock = new Mock<IPharmacyStaffRepository>();
            service = new PharmacyScheduleService(shiftRepositoryMock.Object, staffRepositoryMock.Object);
        }

        private static Shift MakeShift(int id, DateTime start, DateTime end)
        {
            var staff = new Doctor { StaffID = 1, FirstName = "Test", LastName = "Doc" };
            return new Shift(id, staff, "Ward A", start, end, ShiftStatus.SCHEDULED);
        }

        [Fact]
        public async Task GetShiftsAsync_ReturnsShifts_FromRepository()
        {
            var rangeStart = new DateTime(2025, 4, 14);
            var rangeEnd = new DateTime(2025, 4, 15);
            var expected = new List<Shift>
            {
                MakeShift(1, rangeStart.AddHours(8), rangeStart.AddHours(16)),
            };
            shiftRepositoryMock
                .Setup(shiftRepository => shiftRepository.GetAllShifts())
                .Returns(expected);

            var shifts = await service.GetShiftsAsync(1, rangeStart, rangeEnd);

            Assert.Equal(expected.Count, shifts.Count);
            Assert.Equal(expected[0].Id, shifts[0].Id);
        }

        [Fact]
        public async Task GetShiftsAsync_ReturnsEmptyList_WhenNoShiftsInRange()
        {
            var rangeStart = new DateTime(2025, 4, 14);
            var rangeEnd = new DateTime(2025, 4, 15);
            shiftRepositoryMock
                .Setup(shiftRepository => shiftRepository.GetAllShifts())
                .Returns(new List<Shift>());

            var shifts = await service.GetShiftsAsync(99, rangeStart, rangeEnd);

            Assert.Empty(shifts);
        }

        [Fact]
        public async Task GetShiftsAsync_PassesCorrectStaffId_ToRepository()
        {
            var rangeStart = new DateTime(2025, 4, 14);
            var rangeEnd = new DateTime(2025, 4, 21);
            shiftRepositoryMock
                .Setup(shiftRepository => shiftRepository.GetAllShifts())
                .Returns(new List<Shift>());

            await service.GetShiftsAsync(42, rangeStart, rangeEnd);

            shiftRepositoryMock.Verify(shiftRepository => shiftRepository.GetAllShifts(), Times.Once);
        }

        [Fact]
        public async Task GetShiftsAsync_PassesCorrectDateRange_ToRepository()
        {
            var rangeStart = new DateTime(2025, 4, 14);
            var rangeEnd = new DateTime(2025, 4, 21);
            shiftRepositoryMock
                .Setup(shiftRepository => shiftRepository.GetAllShifts())
                .Returns(new List<Shift>());

            await service.GetShiftsAsync(1, rangeStart, rangeEnd);

            shiftRepositoryMock.Verify(
                shiftRepository => shiftRepository.GetAllShifts(),
                Times.Once);
        }

        [Fact]
        public async Task GetShiftsAsync_ReturnsMultipleShifts_WhenRepositoryReturnsMany()
        {
            var rangeStart = new DateTime(2025, 4, 14);
            var rangeEnd = new DateTime(2025, 4, 21);
            var shifts = new List<Shift>
            {
                MakeShift(1, rangeStart.AddHours(8), rangeStart.AddHours(16)),
                MakeShift(2, rangeStart.AddDays(1).AddHours(8), rangeStart.AddDays(1).AddHours(16)),
                MakeShift(3, rangeStart.AddDays(2).AddHours(8), rangeStart.AddDays(2).AddHours(16)),
            };
            shiftRepositoryMock
                .Setup(shiftRepository => shiftRepository.GetAllShifts())
                .Returns(shifts);

            var shiftsResult = await service.GetShiftsAsync(1, rangeStart, rangeEnd);

            Assert.Equal(3, shiftsResult.Count);
        }

        [Fact]
        public void GetPharmacists_DelegatesToStaffRepository()
        {
            var pharmacist = new Pharmacyst(1, "Ana", "Pop", string.Empty, true, "General", 2);
            staffRepositoryMock.Setup(staffRepository => staffRepository.GetPharmacists()).Returns(new List<Pharmacyst> { pharmacist });

            var pharmacysts = service.GetPharmacists();

            Assert.Single(pharmacysts);
            Assert.Equal(1, pharmacysts[0].StaffID);
        }

        [Fact]
        public void GetPharmacists_ReturnsEmptyList_WhenRepositoryReturnsNoPharmacists()
        {
            staffRepositoryMock.Setup(staffRepository => staffRepository.GetPharmacists()).Returns(new List<Pharmacyst>());

            var pharmacysts = service.GetPharmacists();

            Assert.Empty(pharmacysts);
        }
    }
}
