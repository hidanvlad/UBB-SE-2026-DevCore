using DevCoreHospital.Models;
using System;

namespace DevCoreHospital.Services
{
    public interface IFatigueAuditService
    {
        AutoAuditResult RunAutoAudit(DateTime weekStart);

        /// <summary>
        /// Applies a reassignment of the given shift to a new staff member.
        /// Returns true if the repository accepted the change.
        /// </summary>
        bool ReassignShift(int shiftId, int newStaffId);
    }
}

