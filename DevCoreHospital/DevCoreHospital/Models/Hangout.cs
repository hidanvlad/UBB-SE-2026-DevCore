using System;
using System.Collections.Generic;

namespace DevCoreHospital.Models
{
    public class Hangout
    {
        public int hangoutID { get; private set; }
        public string title { get; set; }
        public string description { get; set; }
        public DateTime date { get; set; }
        public string FormattedDate => date.ToString("yyyy-MM-dd");
        public int maxParticipants { get; set; }
        public List<IStaff> participantList { get; }

        public Hangout(int hangoutID, string title, string description, DateTime date, int maxParticipants)
        {
            this.hangoutID = hangoutID;
            this.title = title;
            this.description = description;
            this.date = date;
            this.maxParticipants = maxParticipants;
            this.participantList = new List<IStaff>(this.maxParticipants);
        }
    }
}