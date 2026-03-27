using DevCoreHospital.Data;
using DevCoreHospital.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DevCoreHospital.Services
{
    public sealed class FatigueAuditService : IFatigueAuditService
    {
        private const double MaxWeeklyHours = 60.0;
        private static readonly TimeSpan MinRestGap = TimeSpan.FromHours(12);

        private readonly IFatigueShiftDataSource _dataSource;

        public FatigueAuditService(IFatigueShiftDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public IFatigueShiftDataSource GetDataSource() => _dataSource;

        public AutoAuditResult RunAutoAudit(DateTime weekStart)
        {
            var normalizedWeekStart = StartOfWeek(weekStart);
            var weeklyShifts = _dataSource.GetShiftsForWeek(normalizedWeekStart)
                .OrderBy(s => s.StaffId)
                .ThenBy(s => s.Start)
                .ToList();

            var allShifts = _dataSource.GetAllShifts();
            var staffProfiles = _dataSource.GetStaffProfiles();
            var violations = new List<AuditViolation>();

            foreach (var group in weeklyShifts.GroupBy(s => s.StaffId))
            {
                var staffShifts = group.OrderBy(s => s.Start).ToList();
                var totalHours = staffShifts.Sum(s => (s.End - s.Start).TotalHours);

                if (totalHours > MaxWeeklyHours)
                {
                    foreach (var shift in staffShifts)
                    {
                        violations.Add(new AuditViolation
                        {
                            ShiftId = shift.Id,
                            StaffId = shift.StaffId,
                            StaffName = shift.StaffName,
                            ShiftStart = shift.Start,
                            ShiftEnd = shift.End,
                            Rule = "MAX_60H_PER_WEEK",
                            Message = $"Weekly total is {totalHours:F1}h (limit {MaxWeeklyHours:F0}h)."
                        });
                    }
                }

                for (var i = 1; i < staffShifts.Count; i++)
                {
                    var previous = staffShifts[i - 1];
                    var current = staffShifts[i];
                    var restGap = current.Start - previous.End;

                    if (restGap < MinRestGap)
                    {
                        violations.Add(new AuditViolation
                        {
                            ShiftId = current.Id,
                            StaffId = current.StaffId,
                            StaffName = current.StaffName,
                            ShiftStart = current.Start,
                            ShiftEnd = current.End,
                            Rule = "MIN_12H_REST",
                            Message = $"Rest gap is {restGap.TotalHours:F1}h (minimum {MinRestGap.TotalHours:F0}h)."
                        });
                    }
                }
            }

            var dedupedViolations = violations
                .GroupBy(v => $"{v.ShiftId}:{v.Rule}")
                .Select(g => g.First())
                .OrderBy(v => v.ShiftStart)
                .ToList();

            var suggestions = BuildSuggestions(dedupedViolations, weeklyShifts, allShifts, staffProfiles);

            return new AutoAuditResult
            {
                WeekStart = normalizedWeekStart,
                HasConflicts = dedupedViolations.Count > 0,
                Summary = dedupedViolations.Count == 0
                    ? "No conflicts found. Roster can be published."
                    : $"Found {dedupedViolations.Count} conflict(s). Publishing is blocked until resolved.",
                Violations = dedupedViolations,
                Suggestions = suggestions
            };
        }

        private List<AutoSuggestRecommendation> BuildSuggestions(
            IReadOnlyList<AuditViolation> violations,
            IReadOnlyList<RosterShift> weeklyShifts,
            IReadOnlyList<RosterShift> allShifts,
            IReadOnlyList<StaffProfile> staffProfiles)
        {
            var weeklyById = weeklyShifts.ToDictionary(s => s.Id, s => s);
            var output = new List<AutoSuggestRecommendation>();

            foreach (var violation in violations)
            {
                if (!weeklyById.TryGetValue(violation.ShiftId, out var violatingShift))
                    continue;

                var candidates = staffProfiles
                    .Where(s => s.StaffId != violatingShift.StaffId)
                    .Where(s => string.Equals(s.Role, violatingShift.Role, StringComparison.OrdinalIgnoreCase))
                    .Where(s => string.Equals(s.Specialization, violatingShift.Specialization, StringComparison.OrdinalIgnoreCase))
                    .Where(s => s.IsAvailable == false)
                    .Where(s => !HasOverlap(s.StaffId, violatingShift, allShifts))
                    .OrderBy(s => _dataSource.GetMonthlyWorkedHours(s.StaffId, violatingShift.Start.Year, violatingShift.Start.Month))
                    .ThenBy(s => s.FullName)
                    .ToList();

                var candidate = candidates.FirstOrDefault();
                if (candidate is null)
                {
                    output.Add(new AutoSuggestRecommendation
                    {
                        ShiftId = violatingShift.Id,
                        OriginalStaffId = violatingShift.StaffId,
                        OriginalStaffName = violatingShift.StaffName,
                        SuggestedStaffId = null,
                        SuggestedStaffName = string.Empty,
                        Reason = "No valid replacement found for same role/specialization without overlap."
                    });
                    continue;
                }

                var monthlyHours = _dataSource.GetMonthlyWorkedHours(candidate.StaffId, violatingShift.Start.Year, violatingShift.Start.Month);
                output.Add(new AutoSuggestRecommendation
                {
                    ShiftId = violatingShift.Id,
                    OriginalStaffId = violatingShift.StaffId,
                    OriginalStaffName = violatingShift.StaffName,
                    SuggestedStaffId = candidate.StaffId,
                    SuggestedStaffName = candidate.FullName,
                    Reason = $"Lowest monthly load in matching pool ({monthlyHours:F1}h)."
                });
            }

            return output;
        }

        private static bool HasOverlap(int candidateStaffId, RosterShift proposed, IReadOnlyList<RosterShift> allShifts)
        {
            return allShifts.Any(s =>
                s.StaffId == candidateStaffId &&
                s.Id != proposed.Id &&
                s.Start < proposed.End &&
                s.End > proposed.Start);
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.Date.AddDays(-diff);
        }
    }
}

