using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using DevCoreHospital.Services;

namespace DevCoreHospital.Tests.Integration
{
    public class ShiftSwapFlowIntegrationTests
    {
        private static readonly DateTime FutureShiftStart = DateTime.UtcNow.AddDays(3);
        private static readonly DateTime FutureShiftEnd = FutureShiftStart.AddHours(8);

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

        private sealed class InMemoryShiftSwapRepository : IShiftSwapRepository
        {
            private readonly List<ShiftSwapRequest> requests = new();
            private int nextId = 1;

            public IReadOnlyList<ShiftSwapRequest> Requests => requests;

            public int AddShiftSwapRequest(ShiftSwapRequest request)
            {
                request.SwapId = nextId++;
                requests.Add(request);
                return request.SwapId;
            }

            public IReadOnlyList<ShiftSwapRequest> GetAllShiftSwapRequests() => requests;

            public ShiftSwapRequest? GetShiftSwapRequestById(int swapId)
            {
                bool HasMatchingId(ShiftSwapRequest swapRequest) => swapRequest.SwapId == swapId;
                return requests.FirstOrDefault(HasMatchingId);
            }

            public void UpdateShiftSwapRequestStatus(int swapId, string status)
            {
                var swapRequest = GetShiftSwapRequestById(swapId);
                if (swapRequest != null && Enum.TryParse<ShiftSwapRequestStatus>(status, true, out var parsedStatus))
                {
                    swapRequest.Status = parsedStatus;
                }
            }
        }

        private sealed class InMemoryNotificationRepository : INotificationRepository
        {
            public List<(int RecipientStaffId, string Title, string Message)> Notifications { get; } = new();

            public void AddNotification(int recipientStaffId, string title, string message)
                => Notifications.Add((recipientStaffId, title, message));
        }

        private static Doctor MakeDoctor(int staffId, string specialization = "Cardiology") =>
            new Doctor(staffId, "First", "Last", string.Empty, true, specialization, "LIC", DoctorStatus.AVAILABLE, 1);

        private static (ShiftSwapService Service,
            InMemoryStaffRepository StaffRepo,
            InMemoryShiftRepository ShiftRepo,
            InMemoryShiftSwapRepository SwapRepo,
            InMemoryNotificationRepository NotificationRepo) BuildStack()
        {
            var staffRepository = new InMemoryStaffRepository();
            var shiftRepository = new InMemoryShiftRepository();
            var shiftSwapRepository = new InMemoryShiftSwapRepository();
            var notificationRepository = new InMemoryNotificationRepository();
            var service = new ShiftSwapService(staffRepository, shiftRepository, shiftSwapRepository, notificationRepository);
            return (service, staffRepository, shiftRepository, shiftSwapRepository, notificationRepository);
        }

        private static (Doctor Requester, Doctor Colleague, Shift TargetShift) SeedTwoEligibleDoctors(
            InMemoryStaffRepository staffRepository, InMemoryShiftRepository shiftRepository)
        {
            var requester = MakeDoctor(1);
            var colleague = MakeDoctor(2);
            staffRepository.Members.AddRange(new IStaff[] { requester, colleague });
            var shift = new Shift(10, requester, "ER", FutureShiftStart, FutureShiftEnd, ShiftStatus.SCHEDULED);
            shiftRepository.Shifts.Add(shift);
            return (requester, colleague, shift);
        }

        [Fact]
        public void RequestShiftSwap_WhenColleagueIsEligible_PersistsRequestInPendingState()
        {
            var (service, staffRepository, shiftRepository, shiftSwapRepository, _) = BuildStack();
            var (requester, colleague, shift) = SeedTwoEligibleDoctors(staffRepository, shiftRepository);

            service.RequestShiftSwap(requester.StaffID, shift.Id, colleague.StaffID, out _);

            Assert.Equal(ShiftSwapRequestStatus.PENDING, shiftSwapRepository.Requests.Single().Status);
        }

        [Fact]
        public void RequestShiftSwap_WhenColleagueIsEligible_SendsNotificationToColleague()
        {
            var (service, staffRepository, shiftRepository, _, notificationRepository) = BuildStack();
            var (requester, colleague, shift) = SeedTwoEligibleDoctors(staffRepository, shiftRepository);

            service.RequestShiftSwap(requester.StaffID, shift.Id, colleague.StaffID, out _);

            Assert.Equal(colleague.StaffID, notificationRepository.Notifications.Single().RecipientStaffId);
        }

        [Fact]
        public void AcceptSwapRequest_WhenColleagueAccepts_ReassignsShiftToColleague()
        {
            var (service, staffRepository, shiftRepository, _, _) = BuildStack();
            var (requester, colleague, shift) = SeedTwoEligibleDoctors(staffRepository, shiftRepository);
            service.RequestShiftSwap(requester.StaffID, shift.Id, colleague.StaffID, out _);

            service.AcceptSwapRequest(swapId: 1, colleagueId: colleague.StaffID, out _);

            Assert.Equal(colleague.StaffID, shiftRepository.Shifts.Single().AppointedStaff.StaffID);
        }

        [Fact]
        public void AcceptSwapRequest_WhenColleagueAccepts_NotifiesRequester()
        {
            var (service, staffRepository, shiftRepository, _, notificationRepository) = BuildStack();
            var (requester, colleague, shift) = SeedTwoEligibleDoctors(staffRepository, shiftRepository);
            service.RequestShiftSwap(requester.StaffID, shift.Id, colleague.StaffID, out _);

            service.AcceptSwapRequest(swapId: 1, colleagueId: colleague.StaffID, out _);

            Assert.Equal(requester.StaffID, notificationRepository.Notifications.Last().RecipientStaffId);
        }

        [Fact]
        public void RejectSwapRequest_WhenColleagueRejects_LeavesShiftAssignedToRequester()
        {
            var (service, staffRepository, shiftRepository, _, _) = BuildStack();
            var (requester, colleague, shift) = SeedTwoEligibleDoctors(staffRepository, shiftRepository);
            service.RequestShiftSwap(requester.StaffID, shift.Id, colleague.StaffID, out _);

            service.RejectSwapRequest(swapId: 1, colleagueId: colleague.StaffID, out _);

            Assert.Equal(requester.StaffID, shiftRepository.Shifts.Single().AppointedStaff.StaffID);
        }

        [Fact]
        public void RejectSwapRequest_WhenColleagueRejects_MarksRequestRejected()
        {
            var (service, staffRepository, shiftRepository, shiftSwapRepository, _) = BuildStack();
            var (requester, colleague, shift) = SeedTwoEligibleDoctors(staffRepository, shiftRepository);
            service.RequestShiftSwap(requester.StaffID, shift.Id, colleague.StaffID, out _);

            service.RejectSwapRequest(swapId: 1, colleagueId: colleague.StaffID, out _);

            Assert.Equal(ShiftSwapRequestStatus.REJECTED, shiftSwapRepository.Requests.Single().Status);
        }
    }
}
