using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public class HangoutService
    {
        public HangoutRepository hangoutRepository { get; }

        public HangoutService(HangoutRepository hangoutRepository)
        {
            this.hangoutRepository = hangoutRepository;
        }

        public void CreateHangout(string title, string description, DateTime date, int maxParticipants, IStaff creator)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length < 5 || title.Length > 25)
                throw new ArgumentException("Title must be between 5 and 25 characters.");

            if (description != null && description.Length > 100)
                throw new ArgumentException("Description must be at most 100 characters.");

            if (date.Date < DateTime.Now.Date.AddDays(7))
                throw new ArgumentException("The hangout date must be at least 1 week away from today.");

            // TODO: Validate that the creator has no appointments or medical evaluations scheduled for that day.

            int newId = hangoutRepository.hangoutList.Count > 0 ? hangoutRepository.hangoutList.Max(h => h.hangoutID) + 1 : 1;
            Hangout newHangout = new Hangout(newId, title, description, date, maxParticipants);
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

            if (hangout.participantList.Any(p => p.StaffID == staff.StaffID))
                throw new InvalidOperationException("You have already joined this hangout.");

            // TODO: Validate that the staff member has no appointments or medical evaluations scheduled for that day.

            hangout.participantList.Add(staff);
        }

        public List<Hangout> GetAllHangouts()
        {
            return hangoutRepository.GetAllHangouts();
        }
    }
}