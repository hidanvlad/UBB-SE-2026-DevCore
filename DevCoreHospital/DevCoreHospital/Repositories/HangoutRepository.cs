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
        public List<Hangout> hangoutList;

        public HangoutRepository()
        {
            hangoutList = new List<Hangout> ();
        }

        /// the other methods are assigned to Laszlo
    }
}
