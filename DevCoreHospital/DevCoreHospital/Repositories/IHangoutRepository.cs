using System;
using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IHangoutRepository
    {
        void AddHangout(Hangout hangout);
        void AddParticipant(int hangoutId, int staffId);
        List<Hangout> GetAllHangouts();
        Hangout? GetHangoutById(int id);
        bool HasConflictsOnDate(int staffId, DateTime date);
    }
}
