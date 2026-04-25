using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class FatigueAuditServiceTests
    {
        private static readonly DateTime WeekStart = new DateTime(2025, 4, 14);

        private readonly Mock<IShiftRepository> shiftRepository = new();
        private readonly Mock<IStaffRepository> staffRepository = new();

        public FatigueAuditServiceTests()
        {
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>());
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff>());
        }

        private FatigueAuditService CreateService() =>
            new FatigueAuditService(shiftRepository.Object, staffRepository.Object);

        private static Doctor MakeDoctor(int staffId, string firstName, string specialization) =>
            new Doctor(staffId, firstName, "Last", string.Empty, true, specialization, "LIC", DoctorStatus.AVAILABLE, 1);

        private static Shift MakeShift(int id, IStaff staff, DateTime start, DateTime end, ShiftStatus status = ShiftStatus.SCHEDULED) =>
            new Shift(id, staff, "Ward A", start, end, status);

        [Fact]
        public void RunAutoAudit_ReturnsNoViolations_WhenRosterIsEmpty()
        {
            var roster = CreateService().RunAutoAudit(WeekStart);

            Assert.False(roster.HasConflicts);
            Assert.True(roster.CanPublish);
            Assert.Empty(roster.Violations);
            Assert.Empty(roster.Suggestions);
        }

        [Fact]
        public void RunAutoAudit_NormalizesArbitraryDayToMonday()
        {
            var wednesday = new DateTime(2025, 4, 16);

            var audit = CreateService().RunAutoAudit(wednesday);

            Assert.Equal(new DateTime(2025, 4, 14), audit.WeekStart);
        }

        [Fact]
        public void RunAutoAudit_FlagsMaxWeeklyHoursViolation_WhenStaffWorksMoreThanLimit()
        {
            var doctor = MakeDoctor(1, "Ana", "Cardio");
            var allShifts = new List<Shift>();
            for (int dayIndex = 0; dayIndex < 7; dayIndex++)
            {
                allShifts.Add(MakeShift(dayIndex + 1, doctor, WeekStart.AddDays(dayIndex).AddHours(8), WeekStart.AddDays(dayIndex).AddHours(20)));
            }
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(allShifts);
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff> { doctor });

            var audit = CreateService().RunAutoAudit(WeekStart);

            Assert.True(audit.HasConflicts);
            bool IsMaxWeeklyHoursViolation(AuditViolation violation) => violation.Rule == "MAX_60H_PER_WEEK";
            Assert.Contains(audit.Violations, IsMaxWeeklyHoursViolation);
        }

        [Fact]
        public void RunAutoAudit_FlagsMinRestViolation_WhenGapBetweenShiftsIsBelowMinimum()
        {
            var doctor = MakeDoctor(1, "Ana", "Cardio");
            var firstShift = MakeShift(1, doctor, WeekStart.AddHours(8), WeekStart.AddHours(20));
            var tooSoonAfter = MakeShift(2, doctor, WeekStart.AddHours(22), WeekStart.AddDays(1).AddHours(2));
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { firstShift, tooSoonAfter });
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff> { doctor });

            var audit = CreateService().RunAutoAudit(WeekStart);

            Assert.True(audit.HasConflicts);
            bool IsMinRestViolation(AuditViolation violation) => violation.Rule == "MIN_12H_REST";
            Assert.Contains(audit.Violations, IsMinRestViolation);
        }

        [Fact]
        public void RunAutoAudit_IgnoresCancelledShifts()
        {
            var doctor = MakeDoctor(1, "Ana", "Cardio");
            var cancelled = MakeShift(1, doctor, WeekStart.AddHours(8), WeekStart.AddHours(20), ShiftStatus.CANCELLED);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { cancelled });
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff> { doctor });

            var audit = CreateService().RunAutoAudit(WeekStart);

            Assert.False(audit.HasConflicts);
        }

        [Fact]
        public void ReassignShift_DelegatesToShiftRepository()
        {
            var service = CreateService();

            var isSuccessful = service.ReassignShift(7, 42);

            Assert.True(isSuccessful);
            shiftRepository.Verify(repository => repository.UpdateShiftStaffId(7, 42), Times.Once);
        }

        [Fact]
        public void ReassignShift_ReturnsFalse_WhenInvalidIds()
        {
            var service = CreateService();

            Assert.False(service.ReassignShift(0, 1));
            Assert.False(service.ReassignShift(1, 0));
        }
    }
}
