using DevCoreHospital.Data;
using System;
using System.Linq;

namespace DevCoreHospital.Services
{
    public static class Req3SmokeHarness
    {
        public static string Run()
        {
            var service = new FatigueAuditService(new MockFatigueShiftDataSource());
            var result = service.RunAutoAudit(DateTime.Today);

            var firstViolation = result.Violations.FirstOrDefault();
            var firstSuggestion = result.Suggestions.FirstOrDefault();

            return $"HasConflicts={result.HasConflicts}; " +
                   $"Violations={result.Violations.Count}; " +
                   $"Suggestions={result.Suggestions.Count}; " +
                   $"FirstViolation={(firstViolation == null ? "none" : firstViolation.Rule)}; " +
                   $"FirstSuggestion={(firstSuggestion == null ? "none" : firstSuggestion.Reason)}";
        }
    }
}

