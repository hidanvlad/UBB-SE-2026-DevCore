using DevCoreHospital.Data;
using DevCoreHospital.Models;
using System;

namespace DevCoreHospital.Services
{
    public interface IFatigueAuditService
    {
        AutoAuditResult RunAutoAudit(DateTime weekStart);
        IFatigueShiftDataSource GetDataSource();
    }
}

