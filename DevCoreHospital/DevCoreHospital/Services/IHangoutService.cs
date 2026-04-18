using System;
using System.Collections.Generic;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IHangoutService
    {
        void CreateHangout(string title, string description, DateTime date, int maxParticipants, IStaff creator);
        void JoinHangout(int hangoutId, IStaff staff);
        List<Hangout> GetAllHangouts();
    }
}
