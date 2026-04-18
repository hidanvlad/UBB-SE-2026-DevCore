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

        private readonly HangoutRepository hangoutRepository;

        public HangoutService(HangoutRepository hangoutRepository)
        {
            this.hangoutRepository = hangoutRepository;
        }

        public void CreateHangout(string title, string description, DateTime date, int maxParticipants, IStaff creator)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length < MinHangoutTitleLength || title.Length > MaxHangoutTitleLength)
                throw new ArgumentException($"Title must be between {MinHangoutTitleLength} and {MaxHangoutTitleLength} characters.");

            if (description != null && description.Length > MaxHangoutDescriptionLength)
                throw new ArgumentException($"Description must be at most {MaxHangoutDescriptionLength} characters.");

            if (date.Date < DateTime.Now.Date.AddDays(MinDaysAheadForHangout))
                throw new ArgumentException("The hangout date must be at least 1 week away from today.");

            // Check if the doctor has any non-finished/canceled appointments
            if (hangoutRepository.HasConflictsOnDate(creator.StaffID, date))
                throw new InvalidOperationException("You cannot create a hangout on a day where you have active scheduled appointments.");

            // Provide 0 as a placeholder ID. The database handles real identity ID generation.
            Hangout newHangout = new Hangout(0, title, description, date, maxParticipants);
            newHangout.participantList.Add(creator);

            hangoutRepository.AddHangout(newHangout);
        }

        public void JoinHangout(int hangoutId, IStaff staff)
        {
            var hangout = hangoutRepository.GetHangoutById(hangoutId);
            if (hangout == null)
                throw new ArgumentException("Hangout not found.");

            if (hangout.participantList.Count >= hangout.maxParticipants)
                throw new InvalidOperationException("This hangout is already full.");

            if (hangout.participantList.Any(participant => participant.StaffID == staff.StaffID))
                throw new InvalidOperationException("You have already joined this hangout.");

            // Check if the doctor has any non-finished/canceled appointments
            if (hangoutRepository.HasConflictsOnDate(staff.StaffID, hangout.date))
                throw new InvalidOperationException("You cannot join a hangout on a day where you have active scheduled appointments.");

            hangoutRepository.AddParticipant(hangoutId, staff.StaffID);
        }

        public List<Hangout> GetAllHangouts()
        {
            return hangoutRepository.GetAllHangouts();
        }
    }
}