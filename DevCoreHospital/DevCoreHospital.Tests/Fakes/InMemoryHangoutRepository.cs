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

        public void AddHangout(Hangout hangout)
        {
            var stored = new Hangout(nextId++, hangout.Title, hangout.Description, hangout.Date, hangout.MaxParticipants);
            foreach (var participant in hangout.ParticipantList)
            {
                stored.ParticipantList.Add(participant);
            }
            hangouts.Add(stored);
        }

        public void AddParticipant(int hangoutId, int staffId)
        {
            var hangout = hangouts.FirstOrDefault(h => h.HangoutID == hangoutId);
            if (hangout != null)
            {
                hangout.ParticipantList.Add(new Doctor { StaffID = staffId });
            }
        }

        public List<Hangout> GetAllHangouts() => hangouts.ToList();

        public Hangout? GetHangoutById(int id) => hangouts.FirstOrDefault(h => h.HangoutID == id);

        public bool HasConflictsOnDate(int staffId, DateTime date)
            => conflicts.Contains((staffId, date.Date));

        public void AddConflictForStaff(int staffId, DateTime date)
            => conflicts.Add((staffId, date.Date));
    }
}
