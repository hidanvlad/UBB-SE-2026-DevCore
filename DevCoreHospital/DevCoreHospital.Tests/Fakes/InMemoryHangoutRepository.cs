using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Tests.Fakes
{
    public sealed class InMemoryHangoutRepository : IHangoutRepository
    {
        private readonly List<Hangout> hangouts = new();
        private readonly HashSet<(int staffId, DateTime date)> conflicts = new();
        private int nextId = 1;

        public int AddHangout(Hangout hangout)
        {
            int assignedId = nextId++;
            var stored = new Hangout(assignedId, hangout.Title, hangout.Description, hangout.Date, hangout.MaxParticipants);
            foreach (var participant in hangout.ParticipantList)
            {
                stored.ParticipantList.Add(participant);
            }
            hangouts.Add(stored);
            return assignedId;
        }

        public void AddParticipant(int hangoutId, int staffId)
        {
            bool HasMatchingId(Hangout hangout) => hangout.HangoutID == hangoutId;
            var hangout = hangouts.FirstOrDefault(HasMatchingId);
            if (hangout != null)
            {
                hangout.ParticipantList.Add(new Doctor { StaffID = staffId });
            }
        }

        public List<Hangout> GetAllHangouts() => hangouts.ToList();

        public Hangout? GetHangoutById(int id)
        {
            bool HasMatchingId(Hangout hangout) => hangout.HangoutID == id;
            return hangouts.FirstOrDefault(HasMatchingId);
        }

        public IReadOnlyList<string> GetAppointmentStatusesForStaffOnDate(int staffId, DateTime date)
            => conflicts.Contains((staffId, date.Date))
                ? new List<string> { "Scheduled" }
                : new List<string>();

        public void AddConflictForStaff(int staffId, DateTime date)
            => conflicts.Add((staffId, date.Date));
    }
}
