using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;

namespace DevCoreHospital.Tests.Services
{
    public class ERDispatchServiceTests
    {
        private readonly Mock<IERDispatchRepository> requestRepository = new();
        private readonly Mock<IStaffRepository> staffRepository = new();
        private readonly Mock<IShiftRepository> shiftRepository = new();

        public ERDispatchServiceTests()
        {
            requestRepository.Setup(repository => repository.GetAllRequests()).Returns(new List<ERRequest>());
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff>());
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>());
        }

        private ERDispatchService CreateService() =>
            new ERDispatchService(requestRepository.Object, staffRepository.Object, shiftRepository.Object);

        private static Doctor MakeDoctor(int staffId, string specialization, DoctorStatus status) =>
            new Doctor(staffId, "First", "Last", string.Empty, true, specialization, "LIC", status, 1);

        private static Shift MakeCurrentShift(int id, IStaff staff, string location) =>
            new Shift(id, staff, location, DateTime.Now.AddHours(-1), DateTime.Now.AddHours(2), ShiftStatus.SCHEDULED);

        [Fact]
        public async Task GetPendingRequestIdsAsync_ReturnsOnlyPendingRequests()
        {
            requestRepository.Setup(repository => repository.GetAllRequests()).Returns(new List<ERRequest>
            {
                new ERRequest { Id = 1, Status = "PENDING", CreatedAt = DateTime.Now.AddMinutes(-2) },
                new ERRequest { Id = 2, Status = "ASSIGNED" },
                new ERRequest { Id = 3, Status = "PENDING", CreatedAt = DateTime.Now.AddMinutes(-1) },
            });

            var pendingRequestsIds = await CreateService().GetPendingRequestIdsAsync();

            Assert.Equal(new[] { 1, 3 }, pendingRequestsIds.ToArray());
        }

        [Fact]
        public async Task DispatchERRequestAsync_ReturnsNotFound_WhenRequestIsAbsent()
        {
            requestRepository.Setup(repository => repository.GetAllRequests()).Returns(new List<ERRequest>());

            var erRequestResult = await CreateService().DispatchERRequestAsync(99);

            Assert.False(erRequestResult.IsSuccess);
            Assert.Contains("not found", erRequestResult.Message);
        }

        [Fact]
        public async Task DispatchERRequestAsync_AssignsAvailableMatchingDoctor()
        {
            var request = new ERRequest { Id = 1, Status = "PENDING", Specialization = "Cardio", Location = "ER1" };
            var matchingDoctor = MakeDoctor(7, "Cardio", DoctorStatus.AVAILABLE);
            requestRepository.Setup(repository => repository.GetAllRequests()).Returns(new List<ERRequest> { request });
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff> { matchingDoctor });
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { MakeCurrentShift(1, matchingDoctor, "ER1") });

            var erRequestResult = await CreateService().DispatchERRequestAsync(1);

            Assert.True(erRequestResult.IsSuccess);
            Assert.Equal(7, erRequestResult.MatchedDoctorId);
            requestRepository.Verify(repository => repository.UpdateRequestStatus(1, "ASSIGNED", 7, It.IsAny<string>()), Times.Once);
            staffRepository.Verify(repository => repository.UpdateStatusAsync(7, DoctorStatus.IN_EXAMINATION.ToString()), Times.Once);
        }

        [Fact]
        public async Task DispatchERRequestAsync_MarksUnmatched_WhenNoEligibleDoctor()
        {
            var request = new ERRequest { Id = 1, Status = "PENDING", Specialization = "Cardio", Location = "ER1" };
            var unavailable = MakeDoctor(7, "Cardio", DoctorStatus.IN_EXAMINATION);
            requestRepository.Setup(repository => repository.GetAllRequests()).Returns(new List<ERRequest> { request });
            staffRepository.Setup(repository => repository.LoadAllStaff()).Returns(new List<IStaff> { unavailable });
            shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { MakeCurrentShift(1, unavailable, "ER1") });

            var erRequestResult = await CreateService().DispatchERRequestAsync(1);

            Assert.False(erRequestResult.IsSuccess);
            requestRepository.Verify(repository => repository.UpdateRequestStatus(1, "UNMATCHED", null, null), Times.Once);
        }

        [Fact]
        public async Task SimulateIncomingRequestsAsync_CreatesRequestedNumberOfRequests()
        {
            requestRepository.Setup(repository => repository.AddRequest(It.IsAny<string>(), It.IsAny<string>(), "PENDING")).Returns(1);

            var incomingRequests = await CreateService().SimulateIncomingRequestsAsync(3);

            Assert.Equal(3, incomingRequests.Count);
            requestRepository.Verify(repository => repository.AddRequest(It.IsAny<string>(), It.IsAny<string>(), "PENDING"), Times.Exactly(3));
        }

        [Fact]
        public async Task GetManualOverrideCandidatesAsync_ReturnsEmpty_WhenRequestNotFound()
        {
            requestRepository.Setup(repository => repository.GetRequestById(42)).Returns((ERRequest?)null);

            var candidates = await CreateService().GetManualOverrideCandidatesAsync(42, 30);

            Assert.Empty(candidates);
        }
    }
}
