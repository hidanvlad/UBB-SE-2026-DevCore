using System;
using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;
using Moq;
using Xunit;

namespace DevCoreHospital.Tests.Services;

public class ShiftSwapServiceTests
{
    private readonly Mock<IStaffRepository> staffRepository = new();
    private readonly Mock<IShiftRepository> shiftRepository = new();
    private readonly Mock<IShiftSwapRepository> shiftSwapRepository = new();
    private readonly Mock<INotificationRepository> notificationRepository = new();

    private ShiftSwapService CreateService() =>
        new ShiftSwapService(
            staffRepository.Object,
            shiftRepository.Object,
            shiftSwapRepository.Object,
            notificationRepository.Object);

    private static Doctor BuildDoctor(int staffId, string specialization) =>
        new Doctor(staffId, "First", "Last", "email@example.com", true, specialization, "LIC", DoctorStatus.AVAILABLE, 1);

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenShiftMissing_ReturnsShiftNotFoundError()
    {
        shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift>());

        _ = CreateService().GetEligibleSwapColleaguesForShift(1, 1, out var error);

        Assert.Equal("Shift not found.", error);
    }

    [Fact]
    public void GetEligibleSwapColleaguesForShift_WhenRequesterIsNotAppointed_ReturnsOwnShiftError()
    {
        var appointedDoctor = BuildDoctor(2, "Cardio");
        var targetShift = new Shift(10, appointedDoctor, "ER", DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(8), ShiftStatus.SCHEDULED);
        shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { targetShift });

        _ = CreateService().GetEligibleSwapColleaguesForShift(1, 10, out var error);

        Assert.Equal("You can only request swap for your own shift.", error);
    }

    [Fact]
    public void GetIncomingSwapRequests_ReturnsOnlyPendingRequestsForColleague()
    {
        var pending = new ShiftSwapRequest { SwapId = 5, ColleagueId = 9, Status = ShiftSwapRequestStatus.PENDING, RequestedAt = DateTime.UtcNow };
        var accepted = new ShiftSwapRequest { SwapId = 6, ColleagueId = 9, Status = ShiftSwapRequestStatus.ACCEPTED, RequestedAt = DateTime.UtcNow };
        var pendingForOther = new ShiftSwapRequest { SwapId = 7, ColleagueId = 1, Status = ShiftSwapRequestStatus.PENDING, RequestedAt = DateTime.UtcNow };
        shiftSwapRepository.Setup(repository => repository.GetAllShiftSwapRequests())
            .Returns(new List<ShiftSwapRequest> { pending, accepted, pendingForOther });

        var incomingSwapRequests = CreateService().GetIncomingSwapRequests(9);

        Assert.Single(incomingSwapRequests);
        Assert.Equal(5, incomingSwapRequests[0].SwapId);
    }

    [Fact]
    public void AcceptSwapRequest_WhenSwapIdUnknown_ReturnsNotFoundMessage()
    {
        shiftSwapRepository.Setup(repository => repository.GetShiftSwapRequestById(1)).Returns((ShiftSwapRequest?)null);

        _ = CreateService().AcceptSwapRequest(1, 1, out var message);

        Assert.Equal("Swap request not found.", message);
    }

    [Fact]
    public void AcceptSwapRequest_WhenValid_UpdatesShiftStaffAndStatusAndNotifies()
    {
        var requester = BuildDoctor(3, "Cardio");
        var swapRequest = new ShiftSwapRequest { SwapId = 1, ShiftId = 4, RequesterId = 3, ColleagueId = 2, Status = ShiftSwapRequestStatus.PENDING };
        var targetShift = new Shift(4, requester, "ER", DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(8), ShiftStatus.SCHEDULED);
        shiftSwapRepository.Setup(repository => repository.GetShiftSwapRequestById(1)).Returns(swapRequest);
        shiftRepository.Setup(repository => repository.GetAllShifts()).Returns(new List<Shift> { targetShift });

        var isAccepted = CreateService().AcceptSwapRequest(1, 2, out var message);

        Assert.True(isAccepted);
        Assert.Equal("Swap accepted.", message);
        shiftRepository.Verify(repository => repository.UpdateShiftStaffId(4, 2), Times.Once);
        shiftSwapRepository.Verify(repository => repository.UpdateShiftSwapRequestStatus(1, "ACCEPTED"), Times.Once);
        notificationRepository.Verify(repository => repository.AddNotification(3, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void RejectSwapRequest_WhenValidPending_UpdatesStatusAndNotifies()
    {
        var pending = new ShiftSwapRequest { SwapId = 1, ColleagueId = 2, RequesterId = 3, ShiftId = 4, Status = ShiftSwapRequestStatus.PENDING };
        shiftSwapRepository.Setup(repository => repository.GetShiftSwapRequestById(1)).Returns(pending);

        var isRejected = CreateService().RejectSwapRequest(1, 2, out var message);

        Assert.True(isRejected);
        Assert.Equal("Swap rejected.", message);
        shiftSwapRepository.Verify(repository => repository.UpdateShiftSwapRequestStatus(1, "REJECTED"), Times.Once);
        notificationRepository.Verify(repository => repository.AddNotification(3, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
