using System;
using DevCoreHospital.Models;

namespace DevCoreHospital.Services
{
    public interface IFatigueAuditService
    {
        AutoAuditResult RunAutoAudit(DateTime weekStart);

        bool ReassignShift(int shiftId, int newStaffId);
    }
}

