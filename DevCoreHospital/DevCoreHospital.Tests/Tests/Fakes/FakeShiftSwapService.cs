using System.Collections.Generic;
using DevCoreHospital.Models;
using DevCoreHospital.Services;

namespace DevCoreHospital.Tests.Fakes;

public sealed class FakeShiftSwapService : IShiftSwapService
{
    public List<IStaff> EligibleColleagues { get; } = new();

    public string EligibleError { get; set; } = string.Empty;

    public bool RequestResult { get; set; }

    public string RequestMessage { get; set; } = string.Empty;

    public List<ShiftSwapRequest> PendingInbox { get; } = new();

    public bool AcceptResult { get; set; }

    public string AcceptMessage { get; set; } = string.Empty;

    public bool RejectResult { get; set; }

    public string RejectMessage { get; set; } = string.Empty;

    public bool ReturningEmptyInboxOnSecondGetIncoming { get; set; }

    private int getIncomingQueryCount;

    public List<IStaff> GetEligibleSwapColleaguesForShift(int requesterId, int shiftId, out string error)
    {
        error = EligibleError;
        return EligibleColleagues;
    }

    public bool RequestShiftSwap(int requesterId, int shiftId, int colleagueId, out string message)
    {
        message = RequestMessage;
        return RequestResult;
    }

    public List<ShiftSwapRequest> GetIncomingSwapRequests(int colleagueId)
    {
        getIncomingQueryCount++;
        if (ReturningEmptyInboxOnSecondGetIncoming && getIncomingQueryCount >= 2)
        {
            return new List<ShiftSwapRequest>();
        }

        return new List<ShiftSwapRequest>(PendingInbox);
    }

    public bool AcceptSwapRequest(int swapId, int colleagueId, out string message)
    {
        message = AcceptMessage;
        return AcceptResult;
    }

    public bool RejectSwapRequest(int swapId, int colleagueId, out string message)
    {
        message = RejectMessage;
        return RejectResult;
    }
}
