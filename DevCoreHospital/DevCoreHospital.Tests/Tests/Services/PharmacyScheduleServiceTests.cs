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
        private readonly Mock<IShiftRepository> shiftRepoMock;
        private readonly PharmacyScheduleService sut;

        public PharmacyScheduleServiceTests()
        {
            shiftRepoMock = new Mock<IShiftRepository>();
            sut = new PharmacyScheduleService(shiftRepoMock.Object);
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
            shiftRepoMock
                .Setup(r => r.GetShiftsForStaffInRange(1, rangeStart, rangeEnd))
                .Returns(expected);

            var result = await sut.GetShiftsAsync(1, rangeStart, rangeEnd);

            Assert.Equal(expected.Count, result.Count);
            Assert.Equal(expected[0].Id, result[0].Id);
        }

        [Fact]
        public async Task GetShiftsAsync_ReturnsEmptyList_WhenNoShiftsInRange()
        {
            var rangeStart = new DateTime(2025, 4, 14);
            var rangeEnd = new DateTime(2025, 4, 15);
            shiftRepoMock
                .Setup(r => r.GetShiftsForStaffInRange(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            var result = await sut.GetShiftsAsync(99, rangeStart, rangeEnd);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetShiftsAsync_PassesCorrectStaffId_ToRepository()
        {
            var rangeStart = new DateTime(2025, 4, 14);
            var rangeEnd = new DateTime(2025, 4, 21);
            shiftRepoMock
                .Setup(r => r.GetShiftsForStaffInRange(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            await sut.GetShiftsAsync(42, rangeStart, rangeEnd);

            shiftRepoMock.Verify(r => r.GetShiftsForStaffInRange(42, rangeStart, rangeEnd), Times.Once);
        }

        [Fact]
        public async Task GetShiftsAsync_PassesCorrectDateRange_ToRepository()
        {
            var rangeStart = new DateTime(2025, 4, 14);
            var rangeEnd = new DateTime(2025, 4, 21);
            shiftRepoMock
                .Setup(r => r.GetShiftsForStaffInRange(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<Shift>());

            await sut.GetShiftsAsync(1, rangeStart, rangeEnd);

            shiftRepoMock.Verify(
                r => r.GetShiftsForStaffInRange(It.IsAny<int>(), rangeStart, rangeEnd),
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
            shiftRepoMock
                .Setup(r => r.GetShiftsForStaffInRange(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(shifts);

            var result = await sut.GetShiftsAsync(1, rangeStart, rangeEnd);

            Assert.Equal(3, result.Count);
        }
    }
}
