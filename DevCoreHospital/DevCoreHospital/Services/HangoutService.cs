using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public class HangoutService
    {
        public HangoutRepository hangoutRepository { get; }

        public HangoutService(HangoutRepository hangoutRepository)
        {
            this.hangoutRepository = hangoutRepository;
        }

        // the other methods for this class are assigned to Laszlo
    }
}
