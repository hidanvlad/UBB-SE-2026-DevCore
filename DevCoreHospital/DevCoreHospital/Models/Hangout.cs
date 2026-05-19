using System;
using System.Collections.Generic;

namespace DevCoreHospital.Models
{
    public class Hangout
    {
        private const string DateFormat = "yyyy-MM-dd";

        public int HangoutID { get; private set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Date { get; set; }
        public string FormattedDate => Date.ToString(DateFormat);
        public int MaxParticipants { get; set; }
        public List<IStaff> ParticipantList { get; }

        public Hangout(int hangoutID, string title, string description, DateTime date, int maxParticipants)
        {
            this.HangoutID = hangoutID;
            this.Title = title;
            this.Description = description;
            this.Date = date;
            this.MaxParticipants = maxParticipants;
            this.ParticipantList = new List<IStaff>(this.MaxParticipants);
        }
    }
}