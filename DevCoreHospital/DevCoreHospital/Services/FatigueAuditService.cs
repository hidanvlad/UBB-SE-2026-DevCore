using DevCoreHospital.Models;
using DevCoreHospital.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DevCoreHospital.Services
{
    public sealed class FatigueAuditService : IFatigueAuditService
    {
        private const double MaxWeeklyHours = 60.0;
        private static readonly TimeSpan MinRestGap = TimeSpan.FromHours(12);

        private readonly IFatigueAuditRepository _repository;

        public FatigueAuditService(IFatigueAuditRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public AutoAuditResult RunAutoAudit(DateTime weekStart)
        {
            var normalizedWeekStart = StartOfWeek(weekStart);
            var normalizedWeekEnd = normalizedWeekStart.AddDays(7);

            var allShifts = _repository.GetAllShifts()
                .Where(IsAuditableShift)
                .ToList();

            var weeklyShifts = allShifts
                .Where(s => OverlapsWindow(s, normalizedWeekStart, normalizedWeekEnd))
                .OrderBy(s => s.StaffId)
                .ThenBy(s => s.Start)
                .ToList();

            var staffProfiles = _repository.GetStaffProfiles()
                .Where(IsEligibleStaff)
                .ToList();
            var violations = new List<AuditViolation>();

            foreach (var group in weeklyShifts.GroupBy(s => s.StaffId))
            {
                var staffShifts = group.OrderBy(s => s.Start).ToList();
                var staffAllShifts = allShifts
                    .Where(s => s.StaffId == group.Key)
                    .OrderBy(s => s.Start)
                    .ToList();
                var weeklyShiftIds = staffShifts.Select(s => s.Id).ToHashSet();

                var totalHours = staffShifts.Sum(s => GetOverlapHours(s.Start, s.End, normalizedWeekStart, normalizedWeekEnd));

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

                for (var i = 1; i < staffAllShifts.Count; i++)
                {
                    var previous = staffAllShifts[i - 1];
                    var current = staffAllShifts[i];
                    var restGap = current.Start - previous.End;

                    if (restGap < MinRestGap && weeklyShiftIds.Contains(current.Id))
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

            var suggestions = BuildSuggestions(dedupedViolations, normalizedWeekStart, weeklyShifts, allShifts, staffProfiles);

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
            DateTime weekStart,
            IReadOnlyList<RosterShift> weeklyShifts,
            IReadOnlyList<RosterShift> allShifts,
            IReadOnlyList<StaffProfile> staffProfiles)
        {
            var weeklyById = weeklyShifts.ToDictionary(s => s.Id, s => s);
            var effectiveShifts = allShifts.Select(CloneShift).ToList();
            var output = new List<AutoSuggestRecommendation>();

            foreach (var violation in violations)
            {
                if (!weeklyById.TryGetValue(violation.ShiftId, out var violatingShift))
                    continue;

                var violatingShiftInPlan = effectiveShifts.FirstOrDefault(s => s.Id == violatingShift.Id) ?? violatingShift;
                var strictCandidates = staffProfiles
                    .Where(s => s.StaffId != violatingShiftInPlan.StaffId)
                    .Where(s => string.Equals(s.Role, violatingShiftInPlan.Role, StringComparison.OrdinalIgnoreCase))
                    .Where(s => string.Equals(s.Specialization, violatingShiftInPlan.Specialization, StringComparison.OrdinalIgnoreCase))
                    .Where(IsAvailableForReassignment)
                    .Where(s => CanTakeShift(s.StaffId, violatingShiftInPlan, effectiveShifts, weekStart))
                    .OrderBy(s => GetMonthlyWorkedHoursFromShifts(s.StaffId, violatingShiftInPlan.Start.Year, violatingShiftInPlan.Start.Month, effectiveShifts))
                    .ThenBy(s => s.FullName)
                    .ToList();

                var usedRoleOnlyFallback = false;
                var candidates = strictCandidates;
                if (candidates.Count == 0)
                {
                    usedRoleOnlyFallback = true;
                    candidates = staffProfiles
                        .Where(s => s.StaffId != violatingShiftInPlan.StaffId)
                        .Where(s => string.Equals(s.Role, violatingShiftInPlan.Role, StringComparison.OrdinalIgnoreCase))
                        .Where(IsAvailableForReassignment)
                        .Where(s => CanTakeShift(s.StaffId, violatingShiftInPlan, effectiveShifts, weekStart))
                        .OrderBy(s => GetMonthlyWorkedHoursFromShifts(s.StaffId, violatingShiftInPlan.Start.Year, violatingShiftInPlan.Start.Month, effectiveShifts))
                        .ThenBy(s => s.FullName)
                        .ToList();
                }

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
                        Reason = "No valid replacement found for this role under overlap/rest/hour limits."
                    });
                    continue;
                }

                var monthlyHours = GetMonthlyWorkedHoursFromShifts(candidate.StaffId, violatingShiftInPlan.Start.Year, violatingShiftInPlan.Start.Month, effectiveShifts);
                output.Add(new AutoSuggestRecommendation
                {
                    ShiftId = violatingShiftInPlan.Id,
                    OriginalStaffId = violatingShiftInPlan.StaffId,
                    OriginalStaffName = violatingShiftInPlan.StaffName,
                    SuggestedStaffId = candidate.StaffId,
                    SuggestedStaffName = candidate.FullName,
                    Reason = usedRoleOnlyFallback
                        ? $"Fallback to same role; lowest monthly load in eligible pool ({monthlyHours:F1}h)."
                        : $"Lowest monthly load in matching specialization pool ({monthlyHours:F1}h)."
                });

                ApplyTentativeReassignment(effectiveShifts, violatingShiftInPlan.Id, candidate.StaffId, candidate.FullName);
            }

            return output;
        }

        private static bool CanTakeShift(int candidateStaffId, RosterShift proposed, IReadOnlyList<RosterShift> allShifts, DateTime weekStart)
        {
            if (HasOverlap(candidateStaffId, proposed, allShifts))
                return false;

            var candidateShifts = allShifts
                .Where(s => s.StaffId == candidateStaffId && s.Id != proposed.Id)
                .OrderBy(s => s.Start)
                .ToList();

            var previousShift = candidateShifts.LastOrDefault(s => s.End <= proposed.Start);
            if (previousShift != null && (proposed.Start - previousShift.End) < MinRestGap)
                return false;

            var nextShift = candidateShifts.FirstOrDefault(s => s.Start >= proposed.End);
            if (nextShift != null && (nextShift.Start - proposed.End) < MinRestGap)
                return false;

            var weekEnd = weekStart.AddDays(7);
            var existingHours = candidateShifts.Sum(s => GetOverlapHours(s.Start, s.End, weekStart, weekEnd));
            var proposedHours = GetOverlapHours(proposed.Start, proposed.End, weekStart, weekEnd);
            return existingHours + proposedHours <= MaxWeeklyHours;
        }

        private static void ApplyTentativeReassignment(IList<RosterShift> allShifts, int shiftId, int newStaffId, string newStaffName)
        {
            var shift = allShifts.FirstOrDefault(s => s.Id == shiftId);
            if (shift is null)
                return;

            shift.StaffId = newStaffId;
            shift.StaffName = newStaffName;
        }

        private static RosterShift CloneShift(RosterShift source)
        {
            return new RosterShift
            {
                Id = source.Id,
                StaffId = source.StaffId,
                StaffName = source.StaffName,
                Role = source.Role,
                Specialization = source.Specialization,
                Start = source.Start,
                End = source.End,
                Status = source.Status
            };
        }

        private static bool IsAuditableShift(RosterShift shift)
        {
            return !string.Equals(shift.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEligibleStaff(StaffProfile staff)
        {
            var isInactiveByStatus = string.Equals(staff.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase);
            return staff.IsActive != false && !isInactiveByStatus;
        }

        private static bool IsAvailableForReassignment(StaffProfile staff)
        {
            return staff.IsAvailable != false;
        }

        private static bool OverlapsWindow(RosterShift shift, DateTime windowStart, DateTime windowEnd)
        {
            return shift.Start < windowEnd && shift.End > windowStart;
        }

        private static double GetMonthlyWorkedHoursFromShifts(int staffId, int year, int month, IReadOnlyList<RosterShift> shifts)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1);

            return shifts
                .Where(s => s.StaffId == staffId)
                .Sum(s => GetOverlapHours(s.Start, s.End, monthStart, monthEnd));
        }

        private static double GetOverlapHours(DateTime shiftStart, DateTime shiftEnd, DateTime windowStart, DateTime windowEnd)
        {
            var overlapStart = shiftStart > windowStart ? shiftStart : windowStart;
            var overlapEnd = shiftEnd < windowEnd ? shiftEnd : windowEnd;
            return overlapEnd <= overlapStart ? 0 : (overlapEnd - overlapStart).TotalHours;
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
