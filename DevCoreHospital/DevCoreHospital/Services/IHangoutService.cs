using System.Collections.Generic;
using System;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IHangoutService
    {
        int CreateHangout(string title, string description, DateTime date, int maxParticipants, IStaff creator);
        void JoinHangout(int hangoutId, IStaff staff);
        List<Hangout> GetAllHangouts();
    }
}
