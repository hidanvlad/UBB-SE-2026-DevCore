using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public class HangoutRepository
    {
        public List<Hangout> hangoutList { get; }

        public HangoutRepository()
        {
            hangoutList = new List<Hangout>();
        }

        public void AddHangout(Hangout hangout)
        {
            hangoutList.Add(hangout);
        }

        public List<Hangout> GetAllHangouts()
        {
            return hangoutList.ToList();
        }

        public Hangout? GetHangoutById(int id)
        {
            return hangoutList.FirstOrDefault(h => h.hangoutID == id);
        }
    }
}