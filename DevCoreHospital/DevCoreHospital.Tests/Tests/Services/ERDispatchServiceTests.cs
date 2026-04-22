using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;
using Xunit;

namespace DevCoreHospital.Tests.Services;

public class ERDispatchServiceTests
{
    [Fact]
    public async Task DispatchERRequestAsync_WhenPendingListDoesNotContainRequestId_ReturnsFailureResult()
    {
        var repository = new Mock<IERDispatchRepository>();
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetPendingRequests())
            .Returns(Array.Empty<ERRequest>());
        var service = new ERDispatchService(repository.Object);

        var dispatchResult = await service.DispatchERRequestAsync(42);

        Assert.False(dispatchResult.IsSuccess);
    }

    [Fact]
    public async Task DispatchERRequestAsync_WhenRosterHasAvailableSpecialistInLocation_ReturnsMatchedDoctorName()
    {
        var pendingRequest = new ERRequest { Id = 1, Specialization = "Cardiology", Location = "Ward A" };
        var availableDoctorRosterEntry = new DoctorRosterEntry
        {
            DoctorId = 10,
            FullName = "Dr X",
            Specialization = "Cardiology",
            Location = "Ward A",
            StatusRaw = "AVAILABLE",
            ScheduleStart = DateTime.Now.AddHours(-1),
            ScheduleEnd = DateTime.Now.AddHours(2),
        };
        var repository = new Mock<IERDispatchRepository>();
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetPendingRequests())
            .Returns(new[] { pendingRequest });
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetDoctorRoster())
            .Returns(new[] { availableDoctorRosterEntry });
        var service = new ERDispatchService(repository.Object);

        var dispatchResult = await service.DispatchERRequestAsync(1);

        Assert.Equal("Dr X", dispatchResult.MatchedDoctorName);
    }

    [Fact]
    public async Task DispatchERRequestAsync_WhenNoRosterMatchExists_PersistsUnmatchedStatusInRepository()
    {
        var pendingRequest = new ERRequest { Id = 1, Specialization = "Z99", Location = "Nowhere" };
        string? lastPersistedRequestStatus = null;
        var repository = new Mock<IERDispatchRepository>();
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetPendingRequests())
            .Returns(new[] { pendingRequest });
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetDoctorRoster())
            .Returns(Array.Empty<DoctorRosterEntry>());
        repository
            .Setup(dispatcherRepository => dispatcherRepository.UpdateRequestStatus(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
            .Callback(
                (int requestId, string requestStatus, int? assignedDoctorId, string? assignedDoctorName) => lastPersistedRequestStatus = requestStatus);
        var service = new ERDispatchService(repository.Object);

        await service.DispatchERRequestAsync(1);

        Assert.Equal("UNMATCHED", lastPersistedRequestStatus);
    }

    [Fact]
    public async Task GetManualOverrideCandidatesAsync_WhenGetRequestByIdReturnsNull_ReturnsNoCandidates()
    {
        var repository = new Mock<IERDispatchRepository>();
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetRequestById(1))
            .Returns((ERRequest?)null);
        var service = new ERDispatchService(repository.Object);

        var manualOverrideCandidateProfiles = await service.GetManualOverrideCandidatesAsync(1, 30);

        Assert.Empty(manualOverrideCandidateProfiles);
    }

    [Fact]
    public async Task ManualOverrideAsync_WhenNoNearEndRosterEntryMatchesDoctor_ReturnsUnsuccessfulResult()
    {
        var erRequest = new ERRequest { Id = 1, Specialization = "Cardio", Location = "W1" };
        var overrideDoctorRosterEntry = new DoctorRosterEntry { DoctorId = 5, FullName = "D" };
        var repository = new Mock<IERDispatchRepository>();
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetRequestById(1))
            .Returns(erRequest);
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetDoctorById(5))
            .Returns(overrideDoctorRosterEntry);
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetDoctorRoster())
            .Returns(Array.Empty<DoctorRosterEntry>());
        var service = new ERDispatchService(repository.Object);

        var manualOverrideResult = await service.ManualOverrideAsync(1, 5, 10);

        Assert.False(manualOverrideResult.IsSuccess);
    }

    [Fact]
    public async Task SimulateIncomingRequestsAsync_WhenCountIsZero_StillCreatesAtLeastOne()
    {
        var callCount = 0;
        var repository = new Mock<IERDispatchRepository>();
        repository.Setup(dispatcherRepository => dispatcherRepository.GetDoctorRoster()).Returns(Array.Empty<DoctorRosterEntry>());
        repository
            .Setup(dispatcherRepository => dispatcherRepository.CreateIncomingRequest(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(() => ++callCount);
        var service = new ERDispatchService(repository.Object);

        var createdIds = await service.SimulateIncomingRequestsAsync(0);

        Assert.Equal(1, createdIds.Count);
    }

    [Fact]
    public async Task GetPendingRequestIdsAsync_ReturnsIdFromEachPending()
    {
        var p1 = new ERRequest { Id = 7, Specialization = "A", Location = "B" };
        var p2 = new ERRequest { Id = 8, Specialization = "C", Location = "D" };
        var repository = new Mock<IERDispatchRepository>();
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetPendingRequests())
            .Returns(new[] { p1, p2 });
        var service = new ERDispatchService(repository.Object);

        var ids = await service.GetPendingRequestIdsAsync();

        Assert.Equal(8, ids[1]);
    }

    [Fact]
    public async Task DispatchERRequestAsync_WhenMatchFound_PersistsAssignedInRepository()
    {
        var pending = new ERRequest { Id = 1, Specialization = "Derm", Location = "W1" };
        int? lastDoctorId = null;
        var rosterEntry = new DoctorRosterEntry
        {
            DoctorId = 3,
            FullName = "Dr T",
            Specialization = "Derm",
            Location = "W1",
            StatusRaw = "AVAILABLE",
            ScheduleStart = DateTime.Now.AddHours(-1),
            ScheduleEnd = DateTime.Now.AddHours(1),
        };
        var repository = new Mock<IERDispatchRepository>();
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetPendingRequests())
            .Returns(new[] { pending });
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetDoctorRoster())
            .Returns(new[] { rosterEntry });
        repository
            .Setup(dispatcherRepository => dispatcherRepository.UpdateRequestStatus(1, "ASSIGNED", 3, It.IsAny<string>()))
            .Callback<int, string, int?, string?>(
                (requestId, state, did, dname) => lastDoctorId = did);
        var service = new ERDispatchService(repository.Object);

        _ = await service.DispatchERRequestAsync(1);

        Assert.Equal(3, lastDoctorId);
    }

    [Fact]
    public async Task GetManualOverrideCandidatesAsync_WhenNearEndSpecialistInRoster_ListsSpecialtyMatch()
    {
        var now = DateTime.Now;
        var request = new ERRequest { Id = 1, Specialization = "Onc", Location = "E1" };
        var near = new DoctorRosterEntry
        {
            DoctorId = 4,
            FullName = "Dr R",
            Specialization = "Onc",
            Location = "E1",
            StatusRaw = "IN_EXAMINATION",
            ScheduleStart = now.AddHours(-1),
            ScheduleEnd = now.AddMinutes(20),
        };
        var other = new DoctorRosterEntry
        {
            DoctorId = 5,
            FullName = "X",
            Specialization = "Other",
            Location = "E1",
            StatusRaw = "IN_EXAMINATION",
            ScheduleStart = now.AddHours(-1),
            ScheduleEnd = now.AddMinutes(5),
        };
        var repository = new Mock<IERDispatchRepository>();
        repository.Setup(dispatcherRepository => dispatcherRepository.GetRequestById(1)).Returns(request);
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetDoctorRoster())
            .Returns(new[] { near, other });
        var service = new ERDispatchService(repository.Object);

        var res = await service.GetManualOverrideCandidatesAsync(1, 30);

        Assert.Equal(4, res[0].DoctorId);
    }

    [Fact]
    public async Task ManualOverrideAsync_WhenDoctorIsEligible_UpdatesToAssigned()
    {
        var now = DateTime.Now;
        var erRequest = new ERRequest { Id = 1, Specialization = "Onc", Location = "E1" };
        var near = new DoctorRosterEntry
        {
            DoctorId = 4,
            FullName = "Dr R",
            Specialization = "Onc",
            Location = "E1",
            StatusRaw = "IN_EXAMINATION",
            ScheduleStart = now.AddHours(-1),
            ScheduleEnd = now.AddMinutes(15),
        };
        var repository = new Mock<IERDispatchRepository>();
        repository.Setup(dispatcherRepository => dispatcherRepository.GetRequestById(1)).Returns(erRequest);
        repository
            .Setup(dispatcherRepository => dispatcherRepository.GetDoctorRoster())
            .Returns(new[] { near });
        repository.Setup(dispatcherRepository => dispatcherRepository.GetDoctorById(4)).Returns(near);
        var service = new ERDispatchService(repository.Object);

        var r = await service.ManualOverrideAsync(1, 4, 30);

        Assert.True(r.IsSuccess);
    }
}
