using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public class HangoutService : IHangoutService
    {
        private const int MinHangoutTitleLength = 5;
        private const int MaxHangoutTitleLength = 25;
        private const int MaxHangoutDescriptionLength = 100;
        private const int MinDaysAheadForHangout = 7;
        private const string FinishedAppointmentStatus = "Finished";
        private const string CanceledAppointmentStatusUs = "Canceled";
        private const string CancelledAppointmentStatusUk = "Cancelled";

        private readonly IHangoutRepository hangoutRepository;
        private readonly IHangoutParticipantRepository hangoutParticipantRepository;
        private readonly IAppointmentRepository appointmentRepository;
        private readonly IStaffRepository staffRepository;

        public HangoutService(
            IHangoutRepository hangoutRepository,
            IHangoutParticipantRepository hangoutParticipantRepository,
            IAppointmentRepository appointmentRepository,
            IStaffRepository staffRepository)
        {
            this.hangoutRepository = hangoutRepository;
            this.hangoutParticipantRepository = hangoutParticipantRepository;
            this.appointmentRepository = appointmentRepository;
            this.staffRepository = staffRepository;
        }

        public int CreateHangout(string title, string description, DateTime date, int maxParticipants, IStaff creator)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length < MinHangoutTitleLength || title.Length > MaxHangoutTitleLength)
            {
                throw new ArgumentException($"Title must be between {MinHangoutTitleLength} and {MaxHangoutTitleLength} characters.");
            }

            if (description != null && description.Length > MaxHangoutDescriptionLength)
            {
                throw new ArgumentException($"Description must be at most {MaxHangoutDescriptionLength} characters.");
            }

            if (date.Date < DateTime.Now.Date.AddDays(MinDaysAheadForHangout))
            {
                throw new ArgumentException("The hangout date must be at least 1 week away from today.");
            }

            if (HasConflictingAppointmentOnDate(creator.StaffID, date))
            {
                throw new InvalidOperationException("You cannot create a hangout on a day where you have active scheduled appointments.");
            }

            int newHangoutId = hangoutRepository.AddHangout(title, description ?? string.Empty, date, maxParticipants);
            hangoutParticipantRepository.AddParticipant(newHangoutId, creator.StaffID);
            return newHangoutId;
        }

        public void JoinHangout(int hangoutId, IStaff staff)
        {
            var hangout = hangoutRepository.GetHangoutById(hangoutId);
            if (hangout == null)
            {
                throw new ArgumentException("Hangout not found.");
            }

            bool IsForCurrentHangout((int HangoutId, int StaffId) participant) => participant.HangoutId == hangoutId;
            bool IsCurrentStaffMember((int HangoutId, int StaffId) participant) => participant.StaffId == staff.StaffID;

            var participantsForHangout = hangoutParticipantRepository.GetAllParticipants()
                .Where(IsForCurrentHangout)
                .ToList();

            if (participantsForHangout.Count >= hangout.MaxParticipants)
            {
                throw new InvalidOperationException("This hangout is already full.");
            }

            if (participantsForHangout.Any(IsCurrentStaffMember))
            {
                throw new InvalidOperationException("You have already joined this hangout.");
            }

            if (HasConflictingAppointmentOnDate(staff.StaffID, hangout.Date))
            {
                throw new InvalidOperationException("You cannot join a hangout on a day where you have active scheduled appointments.");
            }

            hangoutParticipantRepository.AddParticipant(hangoutId, staff.StaffID);
        }

        public List<Hangout> GetAllHangouts()
        {
            int ByStaffId(IStaff staffMember) => staffMember.StaffID;

            var hangouts = hangoutRepository.GetAllHangouts();
            var allParticipants = hangoutParticipantRepository.GetAllParticipants();
            var allStaffById = staffRepository.LoadAllStaff().ToDictionary(ByStaffId);

            foreach (var hangout in hangouts)
            {
                bool IsForThisHangout((int HangoutId, int StaffId) participant) => participant.HangoutId == hangout.HangoutID;
                int ToStaffId((int HangoutId, int StaffId) participant) => participant.StaffId;

                var staffIdsForHangout = allParticipants
                    .Where(IsForThisHangout)
                    .Select(ToStaffId);
                foreach (var staffId in staffIdsForHangout)
                {
                    if (allStaffById.TryGetValue(staffId, out var staffMember))
                    {
                        hangout.ParticipantList.Add(staffMember);
                    }
                }
            }
            return hangouts;
        }

        private bool HasConflictingAppointmentOnDate(int staffId, DateTime date)
        {
            System.Threading.Tasks.Task<IReadOnlyList<Appointment>> LoadAllAppointments() => appointmentRepository.GetAllAppointmentsAsync();
            var allAppointments = System.Threading.Tasks.Task.Run(LoadAllAppointments).GetAwaiter().GetResult();

            bool IsActiveStatus(string status) =>
                !string.Equals(status, FinishedAppointmentStatus, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, CanceledAppointmentStatusUs, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, CancelledAppointmentStatusUk, StringComparison.OrdinalIgnoreCase);

            bool IsConflictingForStaff(Appointment appointment) =>
                appointment.DoctorId == staffId
                && appointment.Date.Date == date.Date
                && IsActiveStatus(appointment.Status);

            return allAppointments.Any(IsConflictingForStaff);
        }
    }
}
