using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Data;
using DevCoreHospital.Configuration;

namespace DevCoreHospital.Repositories
{
    public class HangoutRepository
    {
        private readonly DatabaseManager _dbManager;

        public HangoutRepository()
        {
            _dbManager = new DatabaseManager(AppSettings.ConnectionString);
        }

        public void AddHangout(Hangout hangout)
        {
            int newId = _dbManager.InsertHangout(hangout.title, hangout.description, hangout.date, hangout.maxParticipants);

            foreach (var p in hangout.participantList)
            {
                _dbManager.InsertHangoutParticipant(newId, p.StaffID);
            }
        }

        public void AddParticipant(int hangoutId, int staffId)
        {
            _dbManager.InsertHangoutParticipant(hangoutId, staffId);
        }

        public List<Hangout> GetAllHangouts()
        {
            var list = _dbManager.GetAllHangouts();

            foreach (var h in list)
            {
                var participants = _dbManager.GetHangoutParticipants(h.hangoutID);
                h.participantList.AddRange(participants);
            }

            return list;
        }

        public Hangout? GetHangoutById(int id)
        {
            var h = _dbManager.GetHangoutById(id);
            if (h != null)
            {
                var participants = _dbManager.GetHangoutParticipants(h.hangoutID);
                h.participantList.AddRange(participants);
            }
            return h;
        }

        // Conflict Check: Evaluates the business logic locally using raw data from DB
        public bool HasConflictsOnDate(int staffId, DateTime date)
        {
            var statuses = _dbManager.GetAppointmentStatusesForStaffOnDate(staffId, date);

            var activeConflicts = statuses.Where(status =>
                !string.Equals(status, "Finished", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            );

            return activeConflicts.Any();
        }
    }
}