using System.Collections.Generic;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Services;
using DevCoreHospital.ViewModels;
using Moq;
using Xunit;

namespace DevCoreHospital.Tests.ViewModels;

public class ERDispatchViewModelTests
{
    [Fact]
    public void Refresh_ClearsUnmatchedAndSuccessfulCollections()
    {
        var service = new Mock<IERDispatchService>(MockBehavior.Loose);
        var vm = new ERDispatchViewModel(service.Object);
        vm.UnmatchedRequests.Add(new ERDispatchViewModel.UnmatchedRequestRow { RequestId = 1 });
        vm.SuccessfulMatches.Add(new ERDispatchViewModel.SuccessfulMatchRow { RequestId = 2 });
        vm.OverrideCandidates.Add(new ERDispatchViewModel.OverrideCandidateRow { DoctorId = 3 });

        vm.Refresh();

        Assert.Equal(0, vm.UnmatchedRequests.Count + vm.SuccessfulMatches.Count + vm.OverrideCandidates.Count);
    }

    [Fact]
    public async Task SimulateIncomingAsync_SetsStatusMessageMentioningSimulated()
    {
        var service = new Mock<IERDispatchService>();
        service.Setup(dispatchService => dispatchService.SimulateIncomingRequestsAsync(2)).ReturnsAsync((IReadOnlyList<int>)new[] { 1, 2 });
        var vm = new ERDispatchViewModel(service.Object);

        await vm.SimulateIncomingAsync(2);

        Assert.Contains("Simulated", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunDispatchAsync_AddsRowWhenServiceMatches()
    {
        var service = new Mock<IERDispatchService>();
        var erRequest = new ERRequest { Id = 1, Specialization = "A", Location = "L" };
        service.Setup(dispatchService => dispatchService.GetPendingRequestIdsAsync()).ReturnsAsync((IReadOnlyList<int>)new[] { 1 });
        service.Setup(dispatchService => dispatchService.DispatchERRequestAsync(1))
            .ReturnsAsync(new ERDispatchResult { IsSuccess = true, Request = erRequest, MatchedDoctorName = "Doc" });
        var vm = new ERDispatchViewModel(service.Object);

        await vm.RunDispatchAsync();

        Assert.Equal(1, vm.SuccessfulMatches.Count);
    }

    [Fact]
    public async Task RunDispatchAsync_AddsUnmatchedWhenServiceDoesNotMatch()
    {
        var service = new Mock<IERDispatchService>();
        var erRequest = new ERRequest { Id = 1, Specialization = "A", Location = "L" };
        service.Setup(dispatchService => dispatchService.GetPendingRequestIdsAsync()).ReturnsAsync((IReadOnlyList<int>)new[] { 1 });
        service.Setup(dispatchService => dispatchService.DispatchERRequestAsync(1))
            .ReturnsAsync(new ERDispatchResult { IsSuccess = false, Request = erRequest, Message = "no one" });
        service.Setup(dispatchService => dispatchService.GetManualOverrideCandidatesAsync(1, 30))
            .ReturnsAsync((IReadOnlyList<DoctorProfile>)new List<DoctorProfile>());
        var vm = new ERDispatchViewModel(service.Object);

        await vm.RunDispatchAsync();

        Assert.Equal(1, vm.UnmatchedRequests.Count);
    }

    [Fact]
    public async Task LoadOverrideCandidatesAsync_UsesNoEligibleHintWhenListEmpty()
    {
        var service = new Mock<IERDispatchService>();
        service.Setup(dispatchService => dispatchService.GetManualOverrideCandidatesAsync(1, 30))
            .ReturnsAsync((IReadOnlyList<DoctorProfile>)new List<DoctorProfile>());
        var vm = new ERDispatchViewModel(service.Object);

        await vm.LoadOverrideCandidatesAsync(1);

        Assert.Contains("No eligible", vm.ManualInterventionHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyOverrideAsync_FailsWhenUnmatchedIdMissingInCollection()
    {
        var service = new Mock<IERDispatchService>(MockBehavior.Strict);
        var vm = new ERDispatchViewModel(service.Object);
        vm.OverrideCandidates.Add(new ERDispatchViewModel.OverrideCandidateRow { DoctorId = 1 });

        var isOverrideAccepted = await vm.ApplyOverrideAsync(5, 1);

        Assert.False(isOverrideAccepted);
    }

    [Fact]
    public async Task ApplyOverrideAsync_FailsWhenOverrideDoctorNotInCandidatesList()
    {
        var service = new Mock<IERDispatchService>(MockBehavior.Strict);
        var vm = new ERDispatchViewModel(service.Object);
        vm.UnmatchedRequests.Add(new ERDispatchViewModel.UnmatchedRequestRow { RequestId = 1 });
        vm.OverrideCandidates.Clear();

        var isOverrideAccepted = await vm.ApplyOverrideAsync(1, 1);

        Assert.False(isOverrideAccepted);
    }

    [Fact]
    public async Task ApplyOverrideAsync_WhenServiceSucceeds_ReturnsTrue()
    {
        var service = new Mock<IERDispatchService>();
        var manualOverrideErRequest = new ERRequest { Id = 1, Specialization = "S", Location = "L" };
        service.Setup(dispatchService => dispatchService.ManualOverrideAsync(1, 2, 30))
            .ReturnsAsync(
                new ERDispatchResult
                {
                    IsSuccess = true,
                    Request = manualOverrideErRequest,
                    MatchedDoctorName = "Dr Z",
                    MatchReason = "override"
                });
        var vm = new ERDispatchViewModel(service.Object);
        vm.UnmatchedRequests.Add(new ERDispatchViewModel.UnmatchedRequestRow { RequestId = 1, RequestSpecialization = "S", RequestLocation = "L" });
        vm.OverrideCandidates.Add(new ERDispatchViewModel.OverrideCandidateRow { DoctorId = 2, FullName = "Dr Z" });

        var isOverrideAccepted = await vm.ApplyOverrideAsync(1, 2);

        Assert.True(isOverrideAccepted);
    }

    [Fact]
    public async Task LoadOverrideCandidatesAsync_WhenServiceReturnsDoctors_SetsFoundCountInHint()
    {
        var service = new Mock<IERDispatchService>();
        var end = System.DateTime.Now.AddMinutes(20);
        service
            .Setup(dispatchService => dispatchService.GetManualOverrideCandidatesAsync(1, 30))
            .ReturnsAsync(
                (IReadOnlyList<DoctorProfile>)new List<DoctorProfile>
                {
                    new() { DoctorId = 1, FullName = "A", ScheduleEnd = end }
                });
        var vm = new ERDispatchViewModel(service.Object);

        await vm.LoadOverrideCandidatesAsync(1);

        Assert.Contains("Found 1 eligible", vm.ManualInterventionHint, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunDispatchAsync_WhenAllMatched_SetsOverrideNotNeededHint()
    {
        var service = new Mock<IERDispatchService>();
        var erRequest = new ERRequest { Id = 1, Specialization = "A", Location = "L" };
        service.Setup(dispatchService => dispatchService.GetPendingRequestIdsAsync()).ReturnsAsync((IReadOnlyList<int>)new[] { 1 });
        service
            .Setup(dispatchService => dispatchService.DispatchERRequestAsync(1))
            .ReturnsAsync(
                new ERDispatchResult
                {
                    IsSuccess = true,
                    Request = erRequest,
                    MatchedDoctorName = "D"
                });
        var vm = new ERDispatchViewModel(service.Object);

        await vm.RunDispatchAsync();

        Assert.Contains("Override not needed", vm.ManualInterventionHint, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunDispatchAsync_WhenServiceThrows_SetsErrorOnStatus()
    {
        var service = new Mock<IERDispatchService>();
        service
            .Setup(dispatchService => dispatchService.GetPendingRequestIdsAsync())
            .ThrowsAsync(new System.InvalidOperationException("down"));
        var vm = new ERDispatchViewModel(service.Object);

        await vm.RunDispatchAsync();

        Assert.Contains("down", vm.StatusMessage, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulateIncomingAsync_WhenServiceThrows_SetsErrorOnStatus()
    {
        var service = new Mock<IERDispatchService>();
        service
            .Setup(dispatchService => dispatchService.SimulateIncomingRequestsAsync(1))
            .ThrowsAsync(new System.IO.IOException("net"));
        var vm = new ERDispatchViewModel(service.Object);

        await vm.SimulateIncomingAsync(1);

        Assert.Contains("net", vm.StatusMessage, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyOverrideAsync_WhenServiceReturnsNotSuccess_UsesServiceMessageInHint()
    {
        var service = new Mock<IERDispatchService>();
        service
            .Setup(dispatchService => dispatchService.ManualOverrideAsync(1, 2, 30))
            .ReturnsAsync(
                new ERDispatchResult
                {
                    IsSuccess = false,
                    Message = "blocked for unit test"
                });
        var vm = new ERDispatchViewModel(service.Object);
        vm.UnmatchedRequests.Add(new ERDispatchViewModel.UnmatchedRequestRow { RequestId = 1, RequestSpecialization = "A", RequestLocation = "B" });
        vm.OverrideCandidates.Add(new ERDispatchViewModel.OverrideCandidateRow { DoctorId = 2, FullName = "X" });

        _ = await vm.ApplyOverrideAsync(1, 2);

        Assert.Equal("blocked for unit test", vm.ManualInterventionHint);
    }

    [Fact]
    public async Task HandleERRequestAsync_WhenRequestIsNull_DoesNotCallDispatch()
    {
        var service = new Mock<IERDispatchService>(MockBehavior.Strict);
        var vm = new ERDispatchViewModel(service.Object);

        await vm.HandleERRequestAsync(null!);

        service.Verify(
            dispatchService => dispatchService.DispatchERRequestAsync(It.IsAny<int>()),
            Times.Never);
    }
}
