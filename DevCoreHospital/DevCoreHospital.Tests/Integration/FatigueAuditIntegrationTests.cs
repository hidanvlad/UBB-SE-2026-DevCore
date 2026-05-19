using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;

namespace DevCoreHospital.Tests.Integration
{
    public class FatigueAuditIntegrationTests
    {
        private const string MaxWeeklyHoursRule = "MAX_60H_PER_WEEK";
        private const string MinRestRule = "MIN_12H_REST";
        private static readonly DateTime WeekStart = new DateTime(2026, 4, 13);

        private sealed class InMemoryShiftRepository : IShiftRepository
        {
            public List<Shift> Shifts { get; } = new();

            public IReadOnlyList<Shift> GetAllShifts() => Shifts;

            public void AddShift(Shift newShift) => Shifts.Add(newShift);

            public void UpdateShiftStatus(int shiftId, ShiftStatus status) { }

            public void UpdateShiftStaffId(int shiftId, int newStaffId)
            {
                bool HasMatchingId(Shift existing) => existing.Id == shiftId;
                var shift = Shifts.FirstOrDefault(HasMatchingId);
                if (shift != null)
                {
                    shift.AppointedStaff = new Doctor { StaffID = newStaffId };
                }
            }

            public void DeleteShift(int shiftId) { }
        }

        private sealed class InMemoryStaffRepository : IStaffRepository
        {
            public List<IStaff> Members { get; } = new();

            public List<IStaff> LoadAllStaff() => Members;

            public IStaff? GetStaffById(int staffId)
            {
                bool HasMatchingId(IStaff member) => member.StaffID == staffId;
                return Members.FirstOrDefault(HasMatchingId);
            }

            public Task<IReadOnlyList<(int DoctorId, string FirstName, string LastName)>> GetAllDoctorsAsync()
                => Task.FromResult<IReadOnlyList<(int DoctorId, string FirstName, string LastName)>>(new List<(int, string, string)>());

            public Task UpdateStatusAsync(int staffId, string status) => Task.CompletedTask;
        }

        private static (FatigueAuditService Service, InMemoryShiftRepository ShiftRepo, InMemoryStaffRepository StaffRepo) BuildStack()
        {
            var shiftRepository = new InMemoryShiftRepository();
            var staffRepository = new InMemoryStaffRepository();
            var service = new FatigueAuditService(shiftRepository, staffRepository);
            return (service, shiftRepository, staffRepository);
        }

        private static Doctor MakeDoctor(int staffId, string firstName = "Ana", string specialization = "Cardiology") =>
            new Doctor(staffId, firstName, "Pop", string.Empty, true, specialization, "LIC", DoctorStatus.AVAILABLE, 1);

        [Fact]
        public void RunAutoAudit_WhenRosterIsEmpty_ReportsNoConflicts()
        {
            var (service, _, _) = BuildStack();

            var result = service.RunAutoAudit(WeekStart);

            Assert.False(result.HasConflicts);
        }

        [Fact]
        public void RunAutoAudit_WhenStaffWorksMoreThanSixtyHoursInWeek_FlagsMaxWeeklyHoursViolation()
        {
            var (service, shiftRepository, staffRepository) = BuildStack();
            var doctor = MakeDoctor(1);
            staffRepository.Members.Add(doctor);
            for (int dayIndex = 0; dayIndex < 7; dayIndex++)
            {
                shiftRepository.Shifts.Add(new Shift(
                    dayIndex + 1, doctor, "Ward A",
                    WeekStart.AddDays(dayIndex).AddHours(8),
                    WeekStart.AddDays(dayIndex).AddHours(20),
                    ShiftStatus.SCHEDULED));
            }

            var result = service.RunAutoAudit(WeekStart);

            bool IsMaxWeeklyHoursViolation(AuditViolation violation) => violation.Rule == MaxWeeklyHoursRule;
            Assert.Contains(result.Violations, IsMaxWeeklyHoursViolation);
        }

        [Fact]
        public void RunAutoAudit_WhenRestGapBetweenShiftsIsBelowMinimum_FlagsMinRestViolation()
        {
            var (service, shiftRepository, staffRepository) = BuildStack();
            var doctor = MakeDoctor(1);
            staffRepository.Members.Add(doctor);
            shiftRepository.Shifts.Add(new Shift(1, doctor, "Ward A",
                WeekStart.AddHours(8), WeekStart.AddHours(20), ShiftStatus.SCHEDULED));
            shiftRepository.Shifts.Add(new Shift(2, doctor, "Ward A",
                WeekStart.AddHours(22), WeekStart.AddDays(1).AddHours(2), ShiftStatus.SCHEDULED));

            var autoAuditResult = service.RunAutoAudit(WeekStart);

            bool IsMinRestViolation(AuditViolation violation) => violation.Rule == MinRestRule;
            Assert.Contains(autoAuditResult.Violations, IsMinRestViolation);
        }

        [Fact]
        public void RunAutoAudit_WhenSpecialistAvailableForOverloadedShift_SuggestsThatSpecialistAsReplacement()
        {
            var (service, shiftRepository, staffRepository) = BuildStack();
            var overloadedDoctor = MakeDoctor(1, "Ana", "Cardiology");
            var spareSpecialist = MakeDoctor(2, "Bob", "Cardiology");
            staffRepository.Members.AddRange(new IStaff[] { overloadedDoctor, spareSpecialist });
            for (int dayIndex = 0; dayIndex < 7; dayIndex++)
            {
                shiftRepository.Shifts.Add(new Shift(
                    dayIndex + 1, overloadedDoctor, "Ward A",
                    WeekStart.AddDays(dayIndex).AddHours(8),
                    WeekStart.AddDays(dayIndex).AddHours(20),
                    ShiftStatus.SCHEDULED));
            }

            var autoAuditResult = service.RunAutoAudit(WeekStart);

            bool SuggestsSpecialist(AutoSuggestRecommendation suggestion) => suggestion.SuggestedStaffId == spareSpecialist.StaffID;
            Assert.Contains(autoAuditResult.Suggestions, SuggestsSpecialist);
        }

        [Fact]
        public void RunAutoAudit_WhenOnlyShiftIsCancelled_ReportsNoConflicts()
        {
            var (service, shiftRepository, staffRepository) = BuildStack();
            var doctor = MakeDoctor(1);
            staffRepository.Members.Add(doctor);
            shiftRepository.Shifts.Add(new Shift(1, doctor, "Ward A",
                WeekStart.AddHours(8), WeekStart.AddHours(20), ShiftStatus.CANCELLED));

            var autoAuditResult = service.RunAutoAudit(WeekStart);

            Assert.False(autoAuditResult.HasConflicts);
        }

        [Fact]
        public void ReassignShift_WhenIdsAreValid_PersistsNewStaffIdOnTargetShift()
        {
            var (service, shiftRepository, staffRepository) = BuildStack();
            var originalDoctor = MakeDoctor(1);
            var replacementDoctor = MakeDoctor(2);
            staffRepository.Members.AddRange(new IStaff[] { originalDoctor, replacementDoctor });
            shiftRepository.Shifts.Add(new Shift(7, originalDoctor, "Ward A",
                WeekStart.AddHours(8), WeekStart.AddHours(20), ShiftStatus.SCHEDULED));

            service.ReassignShift(7, replacementDoctor.StaffID);

            Assert.Equal(replacementDoctor.StaffID, shiftRepository.Shifts.Single().AppointedStaff.StaffID);
        }
    }
}
