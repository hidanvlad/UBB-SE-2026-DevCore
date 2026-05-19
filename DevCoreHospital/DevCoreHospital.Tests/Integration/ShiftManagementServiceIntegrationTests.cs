using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;

namespace DevCoreHospital.Tests.Integration
{
    public class ShiftManagementServiceIntegrationTests
    {
        private sealed class InMemoryShiftRepository : IShiftManagementShiftRepository
        {
            private readonly List<Shift> shifts;

            public InMemoryShiftRepository(IEnumerable<Shift>? seedShifts = null)
            {
                shifts = seedShifts?.ToList() ?? new List<Shift>();
            }

            public IReadOnlyList<Shift> GetAllShifts() => shifts;

            public void AddShift(Shift newShift) => shifts.Add(newShift);

            public void UpdateShiftStatus(int shiftId, ShiftStatus status)
            {
                bool HasMatchingId(Shift existing) => existing.Id == shiftId;
                var shift = shifts.FirstOrDefault(HasMatchingId);
                if (shift != null)
                {
                    shift.Status = status;
                }
            }
        }

        private sealed class InMemoryStaffRepository : IShiftManagementStaffRepository
        {
            private readonly Dictionary<int, (bool IsAvailable, DoctorStatus Status)> availabilityByStaffId = new();
            private readonly List<IStaff> staff = new();

            public IReadOnlyList<IStaff> Staff => staff;

            public InMemoryStaffRepository AddStaff(IStaff member)
            {
                staff.Add(member);
                return this;
            }

            public List<IStaff> LoadAllStaff() => staff;

            public void UpdateStaffAvailability(int staffId, bool isAvailable, DoctorStatus status = DoctorStatus.OFF_DUTY)
                => availabilityByStaffId[staffId] = (isAvailable, status);

            public void UpdateStaff(IStaff updatedStaff) { }

            public (bool IsAvailable, DoctorStatus Status)? AvailabilityFor(int staffId) =>
                availabilityByStaffId.TryGetValue(staffId, out var snapshot) ? snapshot : null;
        }

        private static Doctor MakeDoctor(int staffId = 10, string specialization = "Cardiology") =>
            new Doctor(staffId, "Ana", "Pop", string.Empty, false, specialization, "LIC", DoctorStatus.OFF_DUTY, 5);

        private static Shift MakeShift(int id, IStaff staff, ShiftStatus status = ShiftStatus.SCHEDULED) =>
            new Shift(id, staff, "Ward A", new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0), status);

        [Fact]
        public void SetShiftActive_WhenShiftExists_PromotesShiftStatusToActive()
        {
            var doctor = MakeDoctor();
            var shift = MakeShift(100, doctor);
            var shiftRepository = new InMemoryShiftRepository(new[] { shift });
            var staffRepository = new InMemoryStaffRepository().AddStaff(doctor);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            service.SetShiftActive(100);

            Assert.Equal(ShiftStatus.ACTIVE, shiftRepository.GetAllShifts().Single().Status);
        }

        [Fact]
        public void SetShiftActive_WhenShiftExists_FlagsAppointedStaffAsAvailable()
        {
            var doctor = MakeDoctor();
            var shiftRepository = new InMemoryShiftRepository(new[] { MakeShift(100, doctor) });
            var staffRepository = new InMemoryStaffRepository().AddStaff(doctor);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            service.SetShiftActive(100);

            Assert.Equal((true, DoctorStatus.AVAILABLE), staffRepository.AvailabilityFor(doctor.StaffID));
        }

        [Fact]
        public void CancelShift_WhenShiftExists_PromotesShiftStatusToCompleted()
        {
            var doctor = MakeDoctor();
            var shiftRepository = new InMemoryShiftRepository(new[] { MakeShift(200, doctor) });
            var staffRepository = new InMemoryStaffRepository().AddStaff(doctor);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            service.CancelShift(200);

            Assert.Equal(ShiftStatus.COMPLETED, shiftRepository.GetAllShifts().Single().Status);
        }

        [Fact]
        public void CancelShift_WhenShiftExists_FlagsAppointedStaffOffDuty()
        {
            var doctor = MakeDoctor();
            var shiftRepository = new InMemoryShiftRepository(new[] { MakeShift(200, doctor) });
            var staffRepository = new InMemoryStaffRepository().AddStaff(doctor);
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            service.CancelShift(200);

            Assert.Equal((false, DoctorStatus.OFF_DUTY), staffRepository.AvailabilityFor(doctor.StaffID));
        }

        [Fact]
        public void TryAddShift_WhenNoOverlap_PersistsShiftInRepository()
        {
            var shiftRepository = new InMemoryShiftRepository();
            var staffRepository = new InMemoryStaffRepository();
            var service = new ShiftManagementService(staffRepository, shiftRepository);
            var doctor = MakeDoctor();

            bool added = service.TryAddShift(doctor, new DateTime(2026, 4, 22, 9, 0, 0), new DateTime(2026, 4, 22, 17, 0, 0), "Ward B");

            Assert.True(added && shiftRepository.GetAllShifts().Count == 1);
        }

        [Fact]
        public void TryAddShift_WhenOverlapsExistingShift_RejectsAndDoesNotPersist()
        {
            var doctor = MakeDoctor();
            var existing = new Shift(1, doctor, "Ward A",
                new DateTime(2026, 4, 22, 9, 0, 0), new DateTime(2026, 4, 22, 17, 0, 0), ShiftStatus.SCHEDULED);
            var shiftRepository = new InMemoryShiftRepository(new[] { existing });
            var staffRepository = new InMemoryStaffRepository();
            var service = new ShiftManagementService(staffRepository, shiftRepository);

            bool added = service.TryAddShift(doctor,
                new DateTime(2026, 4, 22, 12, 0, 0),
                new DateTime(2026, 4, 22, 18, 0, 0),
                "Ward B");

            Assert.False(added);
        }
    }
}
