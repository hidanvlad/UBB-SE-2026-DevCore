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
        private int nextId = 1;

        public int AddHangout(string title, string description, DateTime date, int maxParticipants)
        {
            int assignedId = nextId++;
            hangouts.Add(new Hangout(assignedId, title, description, date, maxParticipants));
            return assignedId;
        }

        public List<Hangout> GetAllHangouts() => hangouts.ToList();

        public Hangout? GetHangoutById(int hangoutId)
        {
            bool HasMatchingId(Hangout hangout) => hangout.HangoutID == hangoutId;
            return hangouts.FirstOrDefault(HasMatchingId);
        }
    }
}
