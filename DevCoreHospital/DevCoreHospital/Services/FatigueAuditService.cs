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

        public bool ReassignShift(int shiftId, int newStaffId)
        {
            return _repository.ReassignShift(shiftId, newStaffId);
        }

        public AutoAuditResult RunAutoAudit(DateTime weekStart)
        {
            var normalizedWeekStart = StartOfWeek(weekStart);
            var normalizedWeekEnd = normalizedWeekStart.AddDays(7);

            var allShifts = _repository.GetAllShifts()
                .Where(IsAuditableShift)
                .ToList();

            var weeklyShifts = allShifts
                .Where(shift => OverlapsWindow(shift, normalizedWeekStart, normalizedWeekEnd))
                .OrderBy(shift => shift.StaffId)
                .ThenBy(shift => shift.Start)
                .ToList();

            var staffProfiles = _repository.GetStaffProfiles()
                .Where(IsEligibleStaff)
                .ToList();
            var violations = new List<AuditViolation>();

            foreach (var group in weeklyShifts.GroupBy(shift => shift.StaffId))
            {
                var staffShifts = group.OrderBy(shift => shift.Start).ToList();
                var staffAllShifts = allShifts
                    .Where(shift => shift.StaffId == group.Key)
                    .OrderBy(shift => shift.Start)
                    .ToList();
                var weeklyShiftIds = staffShifts.Select(shift => shift.Id).ToHashSet();

                var totalHours = staffShifts.Sum(shift => GetOverlapHours(shift.Start, shift.End, normalizedWeekStart, normalizedWeekEnd));

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
                .GroupBy(violation => $"{violation.ShiftId}:{violation.Rule}")
                .Select(violationGroup => violationGroup.First())
                .OrderBy(violation => violation.ShiftStart)
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
            var weeklyById = weeklyShifts.ToDictionary(rosterShift => rosterShift.Id, rosterShift => rosterShift);
            var effectiveShifts = allShifts.Select(CloneShift).ToList();
            var output = new List<AutoSuggestRecommendation>();

            foreach (var violation in violations)
            {
                if (!weeklyById.TryGetValue(violation.ShiftId, out var violatingShift))
                    continue;

                var violatingShiftInPlan = effectiveShifts.FirstOrDefault(s => s.Id == violatingShift.Id) ?? violatingShift;
                var strictCandidates = staffProfiles
                    .Where(staffProfile => staffProfile.StaffId != violatingShiftInPlan.StaffId)
                    .Where(staffProfile => string.Equals(staffProfile.Role, violatingShiftInPlan.Role, StringComparison.OrdinalIgnoreCase))
                    .Where(staffProfile => string.Equals(staffProfile.Specialization, violatingShiftInPlan.Specialization, StringComparison.OrdinalIgnoreCase))
                    .Where(IsAvailableForReassignment)
                    .Where(staffProfile => CanTakeShift(staffProfile.StaffId, violatingShiftInPlan, effectiveShifts, weekStart))
                    .OrderBy(staffProfile => GetMonthlyWorkedHoursFromShifts(staffProfile.StaffId, violatingShiftInPlan.Start.Year, violatingShiftInPlan.Start.Month, effectiveShifts))
                    .ThenBy(staffProfile => staffProfile.FullName)
                    .ToList();

                var usedRoleOnlyFallback = false;
                var candidates = strictCandidates;
                if (candidates.Count == 0)
                {
                    usedRoleOnlyFallback = true;
                    candidates = staffProfiles
                        .Where(staffProfile => staffProfile.StaffId != violatingShiftInPlan.StaffId)
                        .Where(staffProfile => string.Equals(staffProfile.Role, violatingShiftInPlan.Role, StringComparison.OrdinalIgnoreCase))
                        .Where(IsAvailableForReassignment)
                        .Where(staffProfile => CanTakeShift(staffProfile.StaffId, violatingShiftInPlan, effectiveShifts, weekStart))
                        .OrderBy(staffProfile => GetMonthlyWorkedHoursFromShifts(staffProfile.StaffId, violatingShiftInPlan.Start.Year, violatingShiftInPlan.Start.Month, effectiveShifts))
                        .ThenBy(staffProfile => staffProfile.FullName)
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
                .Where(shift => shift.StaffId == candidateStaffId && shift.Id != proposed.Id)
                .OrderBy(shift => shift.Start)
                .ToList();

            var previousShift = candidateShifts.LastOrDefault(shift => shift.End <= proposed.Start);
            if (previousShift != null && (proposed.Start - previousShift.End) < MinRestGap)
                return false;

            var nextShift = candidateShifts.FirstOrDefault(shift => shift.Start >= proposed.End);
            if (nextShift != null && (nextShift.Start - proposed.End) < MinRestGap)
                return false;

            var weekEnd = weekStart.AddDays(7);
            var existingHours = candidateShifts.Sum(shift => GetOverlapHours(shift.Start, shift.End, weekStart, weekEnd));
            var proposedHours = GetOverlapHours(proposed.Start, proposed.End, weekStart, weekEnd);
            return existingHours + proposedHours <= MaxWeeklyHours;
        }

        private static void ApplyTentativeReassignment(IList<RosterShift> allShifts, int shiftId, int newStaffId, string newStaffName)
        {
            var shift = allShifts.FirstOrDefault(rosterShift => rosterShift.Id == shiftId);
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
                .Where(shift => shift.StaffId == staffId)
                .Sum(shift => GetOverlapHours(shift.Start, shift.End, monthStart, monthEnd));
        }

        private static double GetOverlapHours(DateTime shiftStart, DateTime shiftEnd, DateTime windowStart, DateTime windowEnd)
        {
            var overlapStart = shiftStart > windowStart ? shiftStart : windowStart;
            var overlapEnd = shiftEnd < windowEnd ? shiftEnd : windowEnd;
            return overlapEnd <= overlapStart ? 0 : (overlapEnd - overlapStart).TotalHours;
        }

        private static bool HasOverlap(int candidateStaffId, RosterShift proposed, IReadOnlyList<RosterShift> allShifts)
        {
            return allShifts.Any(shift =>
                shift.StaffId == candidateStaffId &&
                shift.Id != proposed.Id &&
                shift.Start < proposed.End &&
                shift.End > proposed.Start);
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            const int daysInWeek = 7;
            var daysFromMonday = (daysInWeek + (date.DayOfWeek - DayOfWeek.Monday)) % daysInWeek;
            return date.Date.AddDays(-daysFromMonday);
        }
    }
}
