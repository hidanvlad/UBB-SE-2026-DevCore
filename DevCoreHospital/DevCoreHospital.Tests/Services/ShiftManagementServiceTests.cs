using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;
using Xunit;

namespace DevCoreHospital.Tests.Services
{
    public class ShiftManagementServiceTests
    {
        private const int NonExistentShiftId = 999;
        private const float ExpectedCombinedShiftHours = 12f;

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
            var shiftId = 100;
            var doctor = BuildDoctor(10, "Cardiology");
            shiftRepository
                .Setup(repository => repository.GetAllShifts())
                .Returns(new List<Shift>
                {
                    BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0))
                });

            int updateCount = 0;
            int updatedShiftId = -1;
            ShiftStatus updatedStatus = ShiftStatus.CANCELLED;

            void CaptureShiftStatusUpdate(int capturedShiftId, ShiftStatus capturedStatus)
            {
                updateCount++;
                updatedShiftId = capturedShiftId;
                updatedStatus = capturedStatus;
            }

            shiftRepository
                .Setup(repository => repository.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback<int, ShiftStatus>(CaptureShiftStatusUpdate);

            service.SetShiftActive(shiftId);

            Assert.Equal((shiftId, ShiftStatus.ACTIVE), (updatedShiftId, updatedStatus));
        }

        [Fact]
        public void SetShiftActive_WhenShiftExists_UpdatesStaffAvailabilityToAvailable()
        {
            var shiftId = 101;
            var doctor = BuildDoctor(11, "Neurology");
            shiftRepository
                .Setup(repository => repository.GetAllShifts())
                .Returns(new List<Shift>
                {
                    BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 9, 0, 0), new DateTime(2026, 4, 21, 17, 0, 0))
                });

            int updateCount = 0;
            int updatedStaffId = -1;
            bool updatedAvailability = false;
            DoctorStatus updatedDoctorStatus = DoctorStatus.IN_EXAMINATION;

            void CaptureStaffAvailabilityUpdate(int capturedStaffId, bool capturedIsAvailable, DoctorStatus capturedStatus)
            {
                updateCount++;
                updatedStaffId = capturedStaffId;
                updatedAvailability = capturedIsAvailable;
                updatedDoctorStatus = capturedStatus;
            }

            staffRepository
                .Setup(repository => repository.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback<int, bool, DoctorStatus>(CaptureStaffAvailabilityUpdate);

            service.SetShiftActive(shiftId);

            Assert.Equal((doctor.StaffID, true, DoctorStatus.AVAILABLE), (updatedStaffId, updatedAvailability, updatedDoctorStatus));
        }

        [Fact]
        public void SetShiftActive_WhenShiftDoesNotExist_DoesNotUpdateShiftStatus()
        {
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>());

            int updateCount = 0;
            shiftRepository
                .Setup(repository => repository.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback(() => updateCount++);

            service.SetShiftActive(NonExistentShiftId);

            Assert.Equal(0, updateCount);
        }

        [Fact]
        public void SetShiftActive_WhenShiftDoesNotExist_DoesNotUpdateStaffAvailability()
        {
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>());

            int updateCount = 0;
            staffRepository
                .Setup(repository => repository.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback(() => updateCount++);

            service.SetShiftActive(NonExistentShiftId);

            Assert.Equal(0, updateCount);
        }

        [Fact]
        public void CancelShift_WhenShiftExists_UpdatesStaffAvailabilityToOffDuty()
        {
            var shiftId = 200;
            var doctor = BuildDoctor(20, "Oncology");
            shiftRepository
                .Setup(repository => repository.GetAllShifts())
                .Returns(new List<Shift>
                {
                    BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0))
                });

            int updateCount = 0;
            int updatedStaffId = -1;
            bool updatedAvailability = true;
            DoctorStatus updatedDoctorStatus = DoctorStatus.AVAILABLE;

            void CaptureStaffAvailabilityUpdate(int capturedStaffId, bool capturedIsAvailable, DoctorStatus capturedStatus)
            {
                updateCount++;
                updatedStaffId = capturedStaffId;
                updatedAvailability = capturedIsAvailable;
                updatedDoctorStatus = capturedStatus;
            }

            staffRepository
                .Setup(repository => repository.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback<int, bool, DoctorStatus>(CaptureStaffAvailabilityUpdate);

            service.CancelShift(shiftId);

            Assert.Equal((doctor.StaffID, false, DoctorStatus.OFF_DUTY), (updatedStaffId, updatedAvailability, updatedDoctorStatus));
        }

        [Fact]
        public void CancelShift_WhenShiftExists_UpdatesShiftStatusToCompleted()
        {
            var shiftId = 201;
            var doctor = BuildDoctor(21, "Cardiology");
            shiftRepository
                .Setup(repository => repository.GetAllShifts())
                .Returns(new List<Shift>
                {
                    BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 10, 0, 0), new DateTime(2026, 4, 21, 18, 0, 0))
                });

            int updateCount = 0;
            int updatedShiftId = -1;
            ShiftStatus updatedStatus = ShiftStatus.SCHEDULED;

            void CaptureShiftStatusUpdate(int capturedShiftId, ShiftStatus capturedStatus)
            {
                updateCount++;
                updatedShiftId = capturedShiftId;
                updatedStatus = capturedStatus;
            }

            shiftRepository
                .Setup(repository => repository.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback<int, ShiftStatus>(CaptureShiftStatusUpdate);

            service.CancelShift(shiftId);

            Assert.Equal((shiftId, ShiftStatus.COMPLETED), (updatedShiftId, updatedStatus));
        }

        [Fact]
        public void CancelShift_WhenShiftDoesNotExist_DoesNotUpdateStaffAvailability()
        {
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>());

            int updateCount = 0;
            staffRepository
                .Setup(repository => repository.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback(() => updateCount++);

            service.CancelShift(NonExistentShiftId);

            Assert.Equal(0, updateCount);
        }

        [Fact]
        public void CancelShift_WhenShiftDoesNotExist_DoesNotUpdateShiftStatus()
        {
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>());

            int updateCount = 0;
            shiftRepository
                .Setup(repository => repository.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback(() => updateCount++);

            service.CancelShift(NonExistentShiftId);

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
            var day = new DateTime(2026, 4, 21);
            var existingDoctor = BuildDoctor(25, "Cardiology");
            var existingShift = BuildShift(1, existingDoctor, day.AddHours(10), day.AddHours(12));
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { existingShift });

            var result = service.ValidateNoOverlap(existingDoctor.StaffID, day.AddHours(candidateStartHour), day.AddHours(candidateEndHour));

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ValidateNoOverlap_WhenShiftsBelongToOtherStaff_ReturnsTrue()
        {
            var day = new DateTime(2026, 4, 21);
            var otherDoctor = BuildDoctor(200, "Neurology");
            var shiftForOtherStaff = BuildShift(10, otherDoctor, day.AddHours(10), day.AddHours(12));
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { shiftForOtherStaff });

            var result = service.ValidateNoOverlap(201, day.AddHours(10), day.AddHours(12));

            Assert.True(result);
        }

        [Theory]
        [InlineData("Pharmacy")]
        [InlineData("pharmacy")]
        [InlineData("PHARMACY")]
        public void GetFilteredStaff_WhenLocationIsPharmacy_ReturnsMatchingPharmacistStaffIds(string location)
        {
            var matchingPharmacist = BuildPharmacyst(1, "Sterile Compounding");
            var nonMatchingPharmacist = BuildPharmacyst(2, "Oncology");
            var doctor = BuildDoctor(3, "Cardiology");
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff>
            {
                matchingPharmacist,
                nonMatchingPharmacist,
                doctor,
            });

            var filteredStaff = service.GetFilteredStaff(location, "sterile");

            Assert.Equal(new[] { matchingPharmacist.StaffID }, filteredStaff.Select(StaffIdSelector).ToArray());
        }

        [Fact]
        public void GetFilteredStaff_WhenLocationIsNotPharmacy_ReturnsMatchingDoctorStaffIds()
        {
            var matchingDoctor = BuildDoctor(10, "Cardiology");
            var nonMatchingDoctor = BuildDoctor(11, "Neurology");
            var pharmacist = BuildPharmacyst(12, "Sterile Compounding");
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff>
            {
                matchingDoctor,
                nonMatchingDoctor,
                pharmacist,
            });

            var filteredStaff = service.GetFilteredStaff("ER", "cardio");

            Assert.Equal(new[] { matchingDoctor.StaffID }, filteredStaff.Select(StaffIdSelector).ToArray());
        }

        [Theory]
        [InlineData("Pharmacy")]
        [InlineData("pharmacy")]
        [InlineData("PHARMACY")]
        public void GetSpecializationsAndCertificationsForLocation_WhenLocationIsPharmacy_ReturnsDistinctSortedNonEmptyCertifications(string location)
        {
            var compounding = BuildPharmacyst(30, "Compounding");
            var toxicology = BuildPharmacyst(31, "Toxicology");
            var duplicateCompounding = BuildPharmacyst(32, "compounding");
            var emptyCertification = BuildPharmacyst(33, string.Empty);
            var nullCertification = BuildPharmacyst(34, "Unused");
            nullCertification.Certification = null!;
            var doctor = BuildDoctor(35, "Cardiology");

            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff>
            {
                compounding,
                toxicology,
                duplicateCompounding,
                emptyCertification,
                nullCertification,
                doctor,
            });

            var result = service.GetSpecializationsAndCertificationsForLocation(location);

            Assert.Equal(new[] { "Compounding", "Toxicology" }, result);
        }

        [Fact]
        public void GetSpecializationsAndCertificationsForLocation_WhenLocationIsNotPharmacy_ReturnsDistinctSortedNonEmptySpecializations()
        {
            var cardiology = BuildDoctor(40, "Cardiology");
            var neurology = BuildDoctor(41, "Neurology");
            var duplicateCardiology = BuildDoctor(42, "cardiology");
            var emptySpecialization = BuildDoctor(43, string.Empty);
            var nullSpecialization = BuildDoctor(44, "Unused");
            nullSpecialization.Specialization = null!;
            var pharmacist = BuildPharmacyst(45, "Compounding");

            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff>
            {
                cardiology,
                neurology,
                duplicateCardiology,
                emptySpecialization,
                nullSpecialization,
                pharmacist,
            });

            var result = service.GetSpecializationsAndCertificationsForLocation("ER");

            Assert.Equal(new[] { "Cardiology", "Neurology" }, result);
        }

        [Fact]
        public void GetSpecializationsAndCertificationsForLocation_WhenNoMatchingNonEmptyValuesExist_ReturnsEmptyList()
        {
            var doctor = BuildDoctor(50, string.Empty);
            var pharmacist = BuildPharmacyst(51, string.Empty);
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff>
            {
                doctor,
                pharmacist,
            });

            var pharmacyResult = service.GetSpecializationsAndCertificationsForLocation("Pharmacy");
            var erResult = service.GetSpecializationsAndCertificationsForLocation("ER");

            Assert.Empty(pharmacyResult);
            Assert.Empty(erResult);
        }

        [Fact]
        public void FindStaffReplacements_WhenShiftIsNull_ReturnsEmptyList()
        {
            var staffReplacements = service.FindStaffReplacements(null!);

            Assert.Empty(staffReplacements);
        }

        [Fact]
        public void FindStaffReplacements_WhenAppointedStaffIsNull_ReturnsEmptyList()
        {
            var shift = BuildShift(90, BuildDoctor(900, "Cardiology"), new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));
            shift.AppointedStaff = null!;

            var staffReplacements = service.FindStaffReplacements(shift);

            Assert.Empty(staffReplacements);
        }

        [Fact]
        public void FindStaffReplacements_WhenCandidatesIncludeOverlapAndDifferentType_ReturnsOnlyCompatibleNonOverlappingStaff()
        {
            var day = new DateTime(2026, 4, 21);
            var currentDoctor = BuildDoctor(1, "Cardiology");
            var candidateNoOverlap = BuildDoctor(2, "Cardiology");
            var candidateOverlap = BuildDoctor(3, "Cardiology");
            var candidateDifferentType = BuildPharmacyst(4, "Sterile Compounding");
            var anotherNoOverlapDoctor = BuildDoctor(5, "Cardiology");

            var targetShift = BuildShift(1000, currentDoctor, day.AddHours(10), day.AddHours(12));
            var conflictingShift = BuildShift(2000, candidateOverlap, day.AddHours(11), day.AddHours(13));

            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff>
            {
                currentDoctor,
                candidateNoOverlap,
                candidateOverlap,
                candidateDifferentType,
                anotherNoOverlapDoctor,
            });

            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>
            {
                targetShift,
                conflictingShift,
            });

            var replacements = service.FindStaffReplacements(targetShift);

            Assert.Equal(
                new[]
                {
                    candidateNoOverlap.StaffID,
                    anotherNoOverlapDoctor.StaffID,
                },
                replacements.Select(StaffIdSelector).ToArray());
        }

        [Fact]
        public void SetShiftActive_WhenExecutingActivationFlow_InvokesExpectedRepositoryCallOrder()
        {
            var shiftId = 500;
            var doctor = BuildDoctor(50, "Emergency Medicine");
            var shift = BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));
            var callOrder = new List<string>();

            List<Shift> GetShiftsAndRecordCall()
            {
                callOrder.Add("GetShifts");
                return new List<Shift> { shift };
            }

            shiftRepository
                .Setup(repository => repository.GetAllShifts())
                .Returns(GetShiftsAndRecordCall);

            shiftRepository
                .Setup(repository => repository.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback(() => callOrder.Add("UpdateShiftStatus"));

            staffRepository
                .Setup(repository => repository.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback(() => callOrder.Add("UpdateStaffAvailability"));

            service.SetShiftActive(shiftId);

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
            var shiftId = 501;
            var doctor = BuildDoctor(51, "Emergency Medicine");
            var shift = BuildShift(shiftId, doctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));
            var callOrder = new List<string>();

            List<Shift> GetShiftsAndRecordCall()
            {
                callOrder.Add("GetShifts");
                return new List<Shift> { shift };
            }

            shiftRepository
                .Setup(repository => repository.GetAllShifts())
                .Returns(GetShiftsAndRecordCall);

            staffRepository
                .Setup(repository => repository.UpdateStaffAvailability(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DoctorStatus>()))
                .Callback(() => callOrder.Add("UpdateStaffAvailability"));

            shiftRepository
                .Setup(repository => repository.UpdateShiftStatus(It.IsAny<int>(), It.IsAny<ShiftStatus>()))
                .Callback(() => callOrder.Add("UpdateShiftStatus"));

            service.CancelShift(shiftId);

            Assert.Equal(
                new[]
                {
                    "GetShifts",
                    "UpdateStaffAvailability",
                    "UpdateShiftStatus",
                },
                callOrder);
        }

        [Fact]
        public void ReassignShift_ReturnsFalse_WhenShiftIsNull()
        {
            var isReassigned = service.ReassignShift(null!, BuildDoctor(60, "Cardiology"));

            Assert.False(isReassigned);
        }

        [Fact]
        public void ReassignShift_ReturnsFalse_WhenNewStaffIsNull()
        {
            var shift = BuildShift(600, BuildDoctor(61, "Cardiology"), new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));

            var isReassigned = service.ReassignShift(shift, null!);

            Assert.False(isReassigned);
        }

        [Fact]
        public void ReassignShift_ReturnsTrue_WhenBothAreValid()
        {
            var shift = BuildShift(601, BuildDoctor(62, "Cardiology"), new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));
            var newStaff = BuildDoctor(63, "Neurology");

            var isReassigned = service.ReassignShift(shift, newStaff);

            Assert.True(isReassigned);
        }

        [Fact]
        public void ReassignShift_UpdatesAppointedStaff_WhenBothAreValid()
        {
            var originalDoctor = BuildDoctor(64, "Cardiology");
            var shift = BuildShift(602, originalDoctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));
            var newStaff = BuildDoctor(65, "Neurology");

            service.ReassignShift(shift, newStaff);

            Assert.Equal(newStaff.StaffID, shift.AppointedStaff.StaffID);
        }

        [Fact]
        public void AddShift_DelegatesToRepository()
        {
            var doctor = BuildDoctor(66, "Cardiology");
            var shift = BuildShift(603, doctor, new DateTime(2026, 4, 21, 8, 0, 0), new DateTime(2026, 4, 21, 16, 0, 0));

            service.AddShift(shift);

            shiftRepository.Verify(repository => repository.AddShift(shift), Times.Once);
        }

        [Fact]
        public void GetDailyShifts_ReturnsOnlyShiftsOnGivenDate()
        {
            var day = new DateTime(2026, 4, 21);
            var doctor = BuildDoctor(67, "Cardiology");
            var matchingShift = BuildShift(700, doctor, day.AddHours(8), day.AddHours(16));
            var differentDayShift = BuildShift(701, doctor, day.AddDays(1).AddHours(8), day.AddDays(1).AddHours(16));
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { matchingShift, differentDayShift });

            var dailyShifts = service.GetDailyShifts(day);

            Assert.Single(dailyShifts);
            Assert.Equal(matchingShift.Id, dailyShifts[0].Id);
        }

        [Fact]
        public void GetDailyShifts_ReturnsEmptyList_WhenNoShiftsOnDate()
        {
            var day = new DateTime(2026, 4, 21);
            var doctor = BuildDoctor(68, "Cardiology");
            var otherDayShift = BuildShift(702, doctor, day.AddDays(1).AddHours(8), day.AddDays(1).AddHours(16));
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { otherDayShift });

            var dailyShifts = service.GetDailyShifts(day);

            Assert.Empty(dailyShifts);
        }

        [Fact]
        public void GetActiveShifts_ReturnsOnlyActiveShifts()
        {
            var day = new DateTime(2026, 4, 21);
            var doctor = BuildDoctor(70, "Cardiology");
            var activeShift = BuildShift(800, doctor, day.AddHours(8), day.AddHours(16), ShiftStatus.ACTIVE);
            var scheduledShift = BuildShift(801, doctor, day.AddHours(16), day.AddHours(20), ShiftStatus.SCHEDULED);
            var cancelledShift = BuildShift(802, doctor, day.AddDays(1).AddHours(8), day.AddDays(1).AddHours(16), ShiftStatus.CANCELLED);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { activeShift, scheduledShift, cancelledShift });

            var activeShifts = service.GetActiveShifts();

            Assert.Single(activeShifts);
            Assert.Equal(activeShift.Id, activeShifts[0].Id);
        }

        [Fact]
        public void GetActiveShifts_WhenNoActiveShifts_ReturnsEmptyList()
        {
            var day = new DateTime(2026, 4, 21);
            var doctor = BuildDoctor(71, "Cardiology");
            var scheduledShift = BuildShift(803, doctor, day.AddHours(8), day.AddHours(16), ShiftStatus.SCHEDULED);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { scheduledShift });

            var activeShifts = service.GetActiveShifts();

            Assert.Empty(activeShifts);
        }

        [Fact]
        public void GetWeeklyHours_WhenShiftsAreInCurrentWeek_ReturnsTotalHours()
        {
            const int nrOfDaysInWeek = 7;
            var now = DateTime.Now;
            int daysFromMonday = (nrOfDaysInWeek + (int)(now.DayOfWeek - DayOfWeek.Monday)) % nrOfDaysInWeek;
            var weekMonday = now.Date.AddDays(-daysFromMonday);
            var doctor = BuildDoctor(72, "Cardiology");
            var shiftOne = BuildShift(810, doctor, weekMonday.AddHours(8), weekMonday.AddHours(16));
            var shiftTwo = BuildShift(811, doctor, weekMonday.AddDays(1).AddHours(8), weekMonday.AddDays(1).AddHours(12));
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { shiftOne, shiftTwo });

            var weeklyHours = service.GetWeeklyHours(doctor.StaffID);

            Assert.Equal(ExpectedCombinedShiftHours, weeklyHours);
        }

        [Fact]
        public void GetWeeklyHours_WhenShiftsAreOutsideCurrentWeek_ReturnsZero()
        {
            var doctor = BuildDoctor(73, "Cardiology");
            var pastShift = BuildShift(812, doctor, DateTime.Now.AddDays(-14).AddHours(8), DateTime.Now.AddDays(-14).AddHours(16));
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { pastShift });

            var weeklyHours = service.GetWeeklyHours(doctor.StaffID);

            Assert.Equal(0f, weeklyHours);
        }

        [Fact]
        public void IsStaffWorkingDuring_WhenScheduledShiftOverlaps_ReturnsTrue()
        {
            var day = DateTime.Now.AddDays(1);
            var doctor = BuildDoctor(74, "Cardiology");
            var shift = BuildShift(820, doctor, day.AddHours(8), day.AddHours(16), ShiftStatus.SCHEDULED);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { shift });

            var isWorking = service.IsStaffWorkingDuring(doctor.StaffID, day.AddHours(10), day.AddHours(12));

            Assert.True(isWorking);
        }

        [Fact]
        public void IsStaffWorkingDuring_WhenNoOverlap_ReturnsFalse()
        {
            var day = DateTime.Now.AddDays(1);
            var doctor = BuildDoctor(75, "Cardiology");
            var shift = BuildShift(821, doctor, day.AddHours(8), day.AddHours(16), ShiftStatus.SCHEDULED);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { shift });

            var isWorking = service.IsStaffWorkingDuring(doctor.StaffID, day.AddHours(17), day.AddHours(19));

            Assert.False(isWorking);
        }

        [Fact]
        public void IsStaffWorkingDuring_WhenShiftIsFinished_ReturnsFalse()
        {
            var day = DateTime.Now.AddDays(1);
            var doctor = BuildDoctor(76, "Cardiology");
            var shift = BuildShift(822, doctor, day.AddHours(8), day.AddHours(16), ShiftStatus.COMPLETED);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { shift });

            var isWorking = service.IsStaffWorkingDuring(doctor.StaffID, day.AddHours(10), day.AddHours(12));

            Assert.False(isWorking);
        }

        [Fact]
        public void TryAddShift_WhenNoOverlap_AddsShiftAndReturnsTrue()
        {
            var doctor = BuildDoctor(90, "Cardiology");
            var start = new DateTime(2030, 7, 1, 8, 0, 0);
            var end = new DateTime(2030, 7, 1, 16, 0, 0);
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>());
            Shift? added = null;

            void CaptureAddedShift(Shift shift) { added = shift; }

            shiftRepository.Setup(repository => repository.AddShift(It.IsAny<Shift>())).Callback<Shift>(CaptureAddedShift);

            bool isAdded = service.TryAddShift(doctor, start, end, "ER");

            Assert.True(isAdded);
            Assert.NotNull(added);
            Assert.Equal(doctor.StaffID, added!.AppointedStaff.StaffID);
            Assert.Equal("ER", added.Location);
            Assert.Equal(ShiftStatus.SCHEDULED, added.Status);
        }

        [Fact]
        public void TryAddShift_WhenOverlapExists_DoesNotAddShiftAndReturnsFalse()
        {
            var doctor = BuildDoctor(91, "Cardiology");
            var existing = BuildShift(900, doctor, new DateTime(2030, 7, 2, 8, 0, 0), new DateTime(2030, 7, 2, 16, 0, 0));
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { existing });
            int addCalls = 0;
            shiftRepository.Setup(repository => repository.AddShift(It.IsAny<Shift>())).Callback(() => addCalls++);

            bool isAdded = service.TryAddShift(doctor, new DateTime(2030, 7, 2, 10, 0, 0), new DateTime(2030, 7, 2, 14, 0, 0), "ER");

            Assert.False(isAdded);
            Assert.Equal(0, addCalls);
        }

        [Fact]
        public void ValidateShiftTimes_ReturnsTrue_WhenEndIsAfterStart()
        {
            var isCorrect = service.ValidateShiftTimes(new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0));

            Assert.True(isCorrect);
        }

        [Fact]
        public void ValidateShiftTimes_ReturnsFalse_WhenEndEqualsStart()
        {
            var isCorrect = service.ValidateShiftTimes(new TimeSpan(8, 0, 0), new TimeSpan(8, 0, 0));

            Assert.False(isCorrect);
        }

        [Fact]
        public void ValidateShiftTimes_ReturnsFalse_WhenEndIsBeforeStart()
        {
            var isCorrect = service.ValidateShiftTimes(new TimeSpan(16, 0, 0), new TimeSpan(8, 0, 0));

            Assert.False(isCorrect);
        }

        private static Shift BuildShift(int id, IStaff appointedStaff, DateTime start, DateTime end, ShiftStatus status = ShiftStatus.SCHEDULED)
            => new Shift(id, appointedStaff, "ER", start, end, status);

        private static Doctor BuildDoctor(int staffId, string specialization)
            => new Doctor(staffId, "John", "Doe", "john.doe@example.com", false, specialization, "LIC-1", DoctorStatus.OFF_DUTY, 5);

        private static Pharmacyst BuildPharmacyst(int staffId, string certification)
            => new Pharmacyst(staffId, "Pharma", "Cist", "pharma@example.com", true, certification, 4);

        private static int StaffIdSelector(IStaff staff) => staff.StaffID;
    }
}