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
    public void Refresh_WhenExecuted_ClearsUnmatchedAndSuccessfulCollections()
    {
        var service = new Mock<IERDispatchService>(MockBehavior.Loose);
        var viewModel = new ERDispatchViewModel(service.Object);
        viewModel.UnmatchedRequests.Add(new ERDispatchViewModel.UnmatchedRequestRow { RequestId = 1 });
        viewModel.SuccessfulMatches.Add(new ERDispatchViewModel.SuccessfulMatchRow { RequestId = 2 });
        viewModel.OverrideCandidates.Add(new ERDispatchViewModel.OverrideCandidateRow { DoctorId = 3 });

        viewModel.Refresh();

        Assert.Equal(0, viewModel.UnmatchedRequests.Count + viewModel.SuccessfulMatches.Count + viewModel.OverrideCandidates.Count);
    }

    [Fact]
    public async Task SimulateIncomingAsync_WhenExecuted_SetsStatusMessageMentioningSimulated()
    {
        var service = new Mock<IERDispatchService>();
        service.Setup(dispatchService => dispatchService.SimulateIncomingRequestsAsync(2)).ReturnsAsync((IReadOnlyList<int>)new[] { 1, 2 });
        var viewModel = new ERDispatchViewModel(service.Object);

        await viewModel.SimulateIncomingAsync(2);

        Assert.Contains("Simulated", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunDispatchAsync_WhenServiceMatches_AddsRow()
    {
        var service = new Mock<IERDispatchService>();
        var erRequest = new ERRequest { Id = 1, Specialization = "A", Location = "L" };
        service.Setup(dispatchService => dispatchService.GetPendingRequestIdsAsync()).ReturnsAsync((IReadOnlyList<int>)new[] { 1 });
        service.Setup(dispatchService => dispatchService.DispatchERRequestAsync(1))
            .ReturnsAsync(new ERDispatchResult { IsSuccess = true, Request = erRequest, MatchedDoctorName = "Doc" });
        var viewModel = new ERDispatchViewModel(service.Object);

        await viewModel.RunDispatchAsync();

        Assert.Single(viewModel.SuccessfulMatches);
    }

    [Fact]
    public async Task RunDispatchAsync_WhenServiceDoesNotMatch_AddsUnmatched()
    {
        var service = new Mock<IERDispatchService>();
        var erRequest = new ERRequest { Id = 1, Specialization = "A", Location = "L" };
        service.Setup(dispatchService => dispatchService.GetPendingRequestIdsAsync()).ReturnsAsync((IReadOnlyList<int>)new[] { 1 });
        service.Setup(dispatchService => dispatchService.DispatchERRequestAsync(1))
            .ReturnsAsync(new ERDispatchResult { IsSuccess = false, Request = erRequest, Message = "no one" });
        service.Setup(dispatchService => dispatchService.GetManualOverrideCandidatesAsync(1, 30))
            .ReturnsAsync((IReadOnlyList<DoctorProfile>)new List<DoctorProfile>());
        var viewModel = new ERDispatchViewModel(service.Object);

        await viewModel.RunDispatchAsync();

        Assert.Single(viewModel.UnmatchedRequests);
    }

    [Fact]
    public async Task LoadOverrideCandidatesAsync_WhenCandidateListIsEmpty_UsesNoEligibleHint()
    {
        var service = new Mock<IERDispatchService>();
        service.Setup(dispatchService => dispatchService.GetManualOverrideCandidatesAsync(1, 30))
            .ReturnsAsync((IReadOnlyList<DoctorProfile>)new List<DoctorProfile>());
        var viewModel = new ERDispatchViewModel(service.Object);

        await viewModel.LoadOverrideCandidatesAsync(1);

        Assert.Contains("No eligible", viewModel.ManualInterventionHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyOverrideAsync_WhenUnmatchedIdMissingFromCollection_Fails()
    {
        var service = new Mock<IERDispatchService>(MockBehavior.Strict);
        var viewModel = new ERDispatchViewModel(service.Object);
        viewModel.OverrideCandidates.Add(new ERDispatchViewModel.OverrideCandidateRow { DoctorId = 1 });

        var isOverrideAccepted = await viewModel.ApplyOverrideAsync(5, 1);

        Assert.False(isOverrideAccepted);
    }

    [Fact]
    public async Task ApplyOverrideAsync_WhenOverrideDoctorNotInCandidatesList_Fails()
    {
        var service = new Mock<IERDispatchService>(MockBehavior.Strict);
        var viewModel = new ERDispatchViewModel(service.Object);
        viewModel.UnmatchedRequests.Add(new ERDispatchViewModel.UnmatchedRequestRow { RequestId = 1 });
        viewModel.OverrideCandidates.Clear();

        var isOverrideAccepted = await viewModel.ApplyOverrideAsync(1, 1);

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
        var viewModel = new ERDispatchViewModel(service.Object);
        viewModel.UnmatchedRequests.Add(new ERDispatchViewModel.UnmatchedRequestRow { RequestId = 1, RequestSpecialization = "S", RequestLocation = "L" });
        viewModel.OverrideCandidates.Add(new ERDispatchViewModel.OverrideCandidateRow { DoctorId = 2, FullName = "Dr Z" });

        var isOverrideAccepted = await viewModel.ApplyOverrideAsync(1, 2);

        Assert.True(isOverrideAccepted);
    }

    [Fact]
    public async Task LoadOverrideCandidatesAsync_WhenServiceReturnsDoctors_SetsFoundCountInHint()
    {
        var service = new Mock<IERDispatchService>();
        var scheduleEnd = System.DateTime.Now.AddMinutes(20);
        service
            .Setup(dispatchService => dispatchService.GetManualOverrideCandidatesAsync(1, 30))
            .ReturnsAsync(
                (IReadOnlyList<DoctorProfile>)new List<DoctorProfile>
                {
                    new() { DoctorId = 1, FullName = "A", ScheduleEnd = scheduleEnd }
                });
        var viewModel = new ERDispatchViewModel(service.Object);

        await viewModel.LoadOverrideCandidatesAsync(1);

        Assert.Contains("Found 1 eligible", viewModel.ManualInterventionHint, System.StringComparison.Ordinal);
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
        var viewModel = new ERDispatchViewModel(service.Object);

        await viewModel.RunDispatchAsync();

        Assert.Contains("Override not needed", viewModel.ManualInterventionHint, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunDispatchAsync_WhenServiceThrows_SetsErrorOnStatus()
    {
        var service = new Mock<IERDispatchService>();
        service
            .Setup(dispatchService => dispatchService.GetPendingRequestIdsAsync())
            .ThrowsAsync(new System.InvalidOperationException("down"));
        var viewModel = new ERDispatchViewModel(service.Object);

        await viewModel.RunDispatchAsync();

        Assert.Contains("down", viewModel.StatusMessage, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulateIncomingAsync_WhenServiceThrows_SetsErrorOnStatus()
    {
        var service = new Mock<IERDispatchService>();
        service
            .Setup(dispatchService => dispatchService.SimulateIncomingRequestsAsync(1))
            .ThrowsAsync(new System.IO.IOException("net"));
        var viewModel = new ERDispatchViewModel(service.Object);

        await viewModel.SimulateIncomingAsync(1);

        Assert.Contains("net", viewModel.StatusMessage, System.StringComparison.Ordinal);
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
        var viewModel = new ERDispatchViewModel(service.Object);
        viewModel.UnmatchedRequests.Add(new ERDispatchViewModel.UnmatchedRequestRow { RequestId = 1, RequestSpecialization = "A", RequestLocation = "B" });
        viewModel.OverrideCandidates.Add(new ERDispatchViewModel.OverrideCandidateRow { DoctorId = 2, FullName = "X" });

        _ = await viewModel.ApplyOverrideAsync(1, 2);

        Assert.Equal("blocked for unit test", viewModel.ManualInterventionHint);
    }

    [Fact]
    public async Task HandleERRequestAsync_WhenRequestIsNull_DoesNotCallDispatch()
    {
        var service = new Mock<IERDispatchService>(MockBehavior.Strict);
        var viewModel = new ERDispatchViewModel(service.Object);

        await viewModel.HandleERRequestAsync(null!);

        service.Verify(
            dispatchService => dispatchService.DispatchERRequestAsync(It.IsAny<int>()),
            Times.Never);
    }
}
