using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevCoreHospital.Models;

namespace DevCoreHospital.Repositories
{
    public class HangoutRepository
    {
        public List<Hangout> hangoutList { get; }

        public HangoutRepository()
        {
            hangoutList = new List<Hangout> ();
        }

        // the other methods for this class are assigned to Laszlo
    }
}
