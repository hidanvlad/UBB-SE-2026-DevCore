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

            foreach (var participant in hangout.participantList)
            {
                _dbManager.InsertHangoutParticipant(newId, participant.StaffID);
            }
        }

        public void AddParticipant(int hangoutId, int staffId)
        {
            _dbManager.InsertHangoutParticipant(hangoutId, staffId);
        }

        public List<Hangout> GetAllHangouts()
        {
            var list = _dbManager.GetAllHangouts();

            foreach (var hangout in list)
            {
                var participants = _dbManager.GetHangoutParticipants(hangout.hangoutID);
                hangout.participantList.AddRange(participants);
            }

            return list;
        }

        public Hangout? GetHangoutById(int id)
        {
            var hangout = _dbManager.GetHangoutById(id);
            if (hangout != null)
            {
                var participants = _dbManager.GetHangoutParticipants(hangout.hangoutID);
                hangout.participantList.AddRange(participants);
            }
            return hangout;
        }

        // Conflict Check: Evaluates the business logic locally using raw data from DB
        public bool HasConflictsOnDate(int staffId, DateTime date)
        {
            var statuses = _dbManager.GetAppointmentStatusesForStaffOnDate(staffId, date);

            var activeConflicts = statuses.Where(appointmentStatus =>
                !string.Equals(appointmentStatus, "Finished", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(appointmentStatus, "Canceled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(appointmentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
            );

            return activeConflicts.Any();
        }
    }
}