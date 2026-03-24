using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace DevCoreHospital.Models
{
    public class Hangout
    {
        public int hangoutID { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public DateAndTime date { get; set; }
        public int maxParticipants { get; set; }
        public List<Staff> participantList { get; set; }

        public Hangout(int hangoutID, string title, string description, DateAndTime date, int maxParticipants)
        {
            this.hangoutID = hangoutID;
            this.title = title;
            this.description = description;
            this.date = date;
            this.maxParticipants = maxParticipants;
            this.participantList = new List<Staff>(this.maxParticipants);
        }
    }
}