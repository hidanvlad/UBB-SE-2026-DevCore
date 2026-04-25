using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public interface IHangoutRepository
    {
        int AddHangout(string title, string description, System.DateTime date, int maxParticipants);
        List<Hangout> GetAllHangouts();
        Hangout? GetHangoutById(int hangoutId);
    }
}
