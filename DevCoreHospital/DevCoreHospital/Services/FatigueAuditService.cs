using System.Collections.Generic;
using System.Linq;
using System;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public sealed class FatigueAuditService : IFatigueAuditService
    {
        private const double MaxWeeklyHours = 60.0;
        private static readonly TimeSpan MinRestGap = TimeSpan.FromHours(12);

        private readonly IFatigueAuditRepository repository;

        public FatigueAuditService(IFatigueAuditRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public bool ReassignShift(int shiftId, int newStaffId)
        {
            if (shiftId <= 0 || newStaffId <= 0)
            {
                return false;
            }

            try
            {
                return repository.UpdateShiftStaffId(shiftId, newStaffId) > 0;
            }
            catch
            {
                return false;
            }
        }

        public AutoAuditResult RunAutoAudit(DateTime weekStart)
        {
            var normalizedWeekStart = StartOfWeek(weekStart);
            var normalizedWeekEnd = normalizedWeekStart.AddDays(7);

            var allShifts = repository.GetAllShifts()
                .Where(IsAuditableShift)
                .ToList();

            bool OverlapsAuditWindow(RosterShift shift) => OverlapsWindow(shift, normalizedWeekStart, normalizedWeekEnd);

            var weeklyShifts = allShifts
                .Where(OverlapsAuditWindow)
                .OrderBy(shift => shift.StaffId)
                .ThenBy(shift => shift.Start)
                .ToList();

            var staffProfiles = repository.GetStaffProfiles()
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

                for (var shiftIndex = 1; shiftIndex < staffAllShifts.Count; shiftIndex++)
                {
                    var previousShift = staffAllShifts[shiftIndex - 1];
                    var currentShift = staffAllShifts[shiftIndex];
                    var restGap = currentShift.Start - previousShift.End;

                    if (restGap < MinRestGap && weeklyShiftIds.Contains(currentShift.Id))
                    {
                        violations.Add(new AuditViolation
                        {
                            ShiftId = currentShift.Id,
                            StaffId = currentShift.StaffId,
                            StaffName = currentShift.StaffName,
                            ShiftStart = currentShift.Start,
                            ShiftEnd = currentShift.End,
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
            var weeklyShiftsById = weeklyShifts.ToDictionary(rosterShift => rosterShift.Id, rosterShift => rosterShift);
            var effectiveShifts = allShifts.Select(CloneShift).ToList();
            var recommendations = new List<AutoSuggestRecommendation>();

            foreach (var violation in violations)
            {
                if (!weeklyShiftsById.TryGetValue(violation.ShiftId, out var violatingShift))
                {
                    continue;
                }

                var violatingShiftInPlan = effectiveShifts.FirstOrDefault(existingShift => existingShift.Id == violatingShift.Id) ?? violatingShift;

                bool IsNotCurrentStaff(StaffProfile staffProfile) => staffProfile.StaffId != violatingShiftInPlan.StaffId;
                bool HasMatchingRole(StaffProfile staffProfile) => string.Equals(staffProfile.Role, violatingShiftInPlan.Role, StringComparison.OrdinalIgnoreCase);
                bool HasMatchingSpecialization(StaffProfile staffProfile) => string.Equals(staffProfile.Specialization, violatingShiftInPlan.Specialization, StringComparison.OrdinalIgnoreCase);
                bool CanTakeViolatingShift(StaffProfile staffProfile) => CanTakeShift(staffProfile.StaffId, violatingShiftInPlan, effectiveShifts, weekStart);
                double GetMonthlyHours(StaffProfile staffProfile) => GetMonthlyWorkedHoursFromShifts(staffProfile.StaffId, violatingShiftInPlan.Start.Year, violatingShiftInPlan.Start.Month, effectiveShifts);

                var matchingSpecializationCandidates = staffProfiles
                    .Where(IsNotCurrentStaff)
                    .Where(HasMatchingRole)
                    .Where(HasMatchingSpecialization)
                    .Where(IsAvailableForReassignment)
                    .Where(CanTakeViolatingShift)
                    .OrderBy(GetMonthlyHours)
                    .ThenBy(staffProfile => staffProfile.FullName)
                    .ToList();

                var isUsingRoleOnlyFallback = false;
                var candidateStaff = matchingSpecializationCandidates;
                if (candidateStaff.Count == 0)
                {
                    isUsingRoleOnlyFallback = true;
                    candidateStaff = staffProfiles
                        .Where(IsNotCurrentStaff)
                        .Where(HasMatchingRole)
                        .Where(IsAvailableForReassignment)
                        .Where(CanTakeViolatingShift)
                        .OrderBy(GetMonthlyHours)
                        .ThenBy(staffProfile => staffProfile.FullName)
                        .ToList();
                }

                var bestCandidate = candidateStaff.FirstOrDefault();
                if (bestCandidate is null)
                {
                    recommendations.Add(new AutoSuggestRecommendation
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

                var monthlyHours = GetMonthlyWorkedHoursFromShifts(bestCandidate.StaffId, violatingShiftInPlan.Start.Year, violatingShiftInPlan.Start.Month, effectiveShifts);
                recommendations.Add(new AutoSuggestRecommendation
                {
                    ShiftId = violatingShiftInPlan.Id,
                    OriginalStaffId = violatingShiftInPlan.StaffId,
                    OriginalStaffName = violatingShiftInPlan.StaffName,
                    SuggestedStaffId = bestCandidate.StaffId,
                    SuggestedStaffName = bestCandidate.FullName,
                    Reason = isUsingRoleOnlyFallback
                        ? $"Fallback to same role; lowest monthly load in eligible pool ({monthlyHours:F1}h)."
                        : $"Lowest monthly load in matching specialization pool ({monthlyHours:F1}h)."
                });

                ApplyTentativeReassignment(effectiveShifts, violatingShiftInPlan.Id, bestCandidate.StaffId, bestCandidate.FullName);
            }

            return recommendations;
        }

        private static bool CanTakeShift(int candidateStaffId, RosterShift proposed, IReadOnlyList<RosterShift> allShifts, DateTime weekStart)
        {
            if (HasOverlap(candidateStaffId, proposed, allShifts))
            {
                return false;
            }

            bool IsForCandidate(RosterShift shift) => shift.StaffId == candidateStaffId && shift.Id != proposed.Id;

            var candidateShifts = allShifts
                .Where(IsForCandidate)
                .OrderBy(shift => shift.Start)
                .ToList();

            bool EndsBeforeProposedStart(RosterShift shift) => shift.End <= proposed.Start;
            var previousShift = candidateShifts.LastOrDefault(EndsBeforeProposedStart);
            if (previousShift != null && (proposed.Start - previousShift.End) < MinRestGap)
            {
                return false;
            }

            bool StartsAfterProposedEnd(RosterShift shift) => shift.Start >= proposed.End;
            var nextShift = candidateShifts.FirstOrDefault(StartsAfterProposedEnd);
            if (nextShift != null && (nextShift.Start - proposed.End) < MinRestGap)
            {
                return false;
            }

            var weekEnd = weekStart.AddDays(7);
            var existingHours = candidateShifts.Sum(shift => GetOverlapHours(shift.Start, shift.End, weekStart, weekEnd));
            var proposedHours = GetOverlapHours(proposed.Start, proposed.End, weekStart, weekEnd);
            return existingHours + proposedHours <= MaxWeeklyHours;
        }

        private static void ApplyTentativeReassignment(IList<RosterShift> allShifts, int shiftId, int newStaffId, string newStaffName)
        {
            bool HasMatchingId(RosterShift rosterShift) => rosterShift.Id == shiftId;
            var shift = allShifts.FirstOrDefault(HasMatchingId);
            if (shift is null)
            {
                return;
            }

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

            bool IsForStaff(RosterShift shift) => shift.StaffId == staffId;

            return shifts
                .Where(IsForStaff)
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
            bool OverlapsWithCandidate(RosterShift shift) =>
                shift.StaffId == candidateStaffId &&
                shift.Id != proposed.Id &&
                shift.Start < proposed.End &&
                shift.End > proposed.Start;

            return allShifts.Any(OverlapsWithCandidate);
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            const int daysInWeek = 7;
            var daysFromMonday = (daysInWeek + (date.DayOfWeek - DayOfWeek.Monday)) % daysInWeek;
            return date.Date.AddDays(-daysFromMonday);
        }
    }
}
