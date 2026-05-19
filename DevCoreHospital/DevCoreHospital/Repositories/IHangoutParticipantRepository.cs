using System.Collections.Generic;

namespace DevCoreHospital.Repositories
{
    public interface IHangoutParticipantRepository
    {
        IReadOnlyList<(int HangoutId, int StaffId)> GetAllParticipants();
        void AddParticipant(int hangoutId, int staffId);
    }
}
