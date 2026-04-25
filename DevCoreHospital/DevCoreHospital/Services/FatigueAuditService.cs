using System;
using System.Collections.Generic;
using System.Linq;
using DevCoreHospital.Models;
using DevCoreHospital.Repositories;

namespace DevCoreHospital.Services
{
    public sealed class FatigueAuditService : IFatigueAuditService
    {
        private const double MaxWeeklyHours = 60.0;
        private const string DoctorRole = "Doctor";
        private const string PharmacistRole = "Pharmacist";
        private const string DefaultSpecialization = "General";
        private const string CancelledShiftStatus = "CANCELLED";
        private const string InactiveStaffStatus = "INACTIVE";
        private const string AvailableStaffStatus = "AVAILABLE";
        private const string MaxWeeklyHoursViolationRule = "MAX_60H_PER_WEEK";
        private const string MinRestViolationRule = "MIN_12H_REST";
        private const int DaysInWeek = 7;
        private const int MinValidId = 1;
        private static readonly TimeSpan MinRestGap = TimeSpan.FromHours(12);

        private readonly IShiftRepository shiftRepository;
        private readonly IStaffRepository staffRepository;

        public FatigueAuditService(IShiftRepository shiftRepository, IStaffRepository staffRepository)
        {
            this.shiftRepository = shiftRepository ?? throw new ArgumentNullException(nameof(shiftRepository));
            this.staffRepository = staffRepository ?? throw new ArgumentNullException(nameof(staffRepository));
        }

        public bool ReassignShift(int shiftId, int newStaffId)
        {
            if (shiftId < MinValidId || newStaffId < MinValidId)
            {
                return false;
            }

            shiftRepository.UpdateShiftStaffId(shiftId, newStaffId);
            return true;
        }

        public AutoAuditResult RunAutoAudit(DateTime weekStart)
        {
            var normalizedWeekStart = StartOfWeek(weekStart);
            var normalizedWeekEnd = normalizedWeekStart.AddDays(DaysInWeek);

            var staffProfilesById = BuildStaffProfilesById();
            var allShifts = BuildAllRosterShifts(staffProfilesById)
                .Where(IsAuditableShift)
                .ToList();

            bool OverlapsAuditWindow(RosterShift shift) =>
                OverlapsWindow(shift, normalizedWeekStart, normalizedWeekEnd);

            int ByStaffId(RosterShift shift) => shift.StaffId;
            DateTime ByStart(RosterShift shift) => shift.Start;

            var weeklyShifts = allShifts
                .Where(OverlapsAuditWindow)
                .OrderBy(ByStaffId)
                .ThenBy(ByStart)
                .ToList();

            var eligibleStaffProfiles = staffProfilesById.Values
                .Where(IsEligibleStaff)
                .ToList();

            var violations = new List<AuditViolation>();

            foreach (var staffShiftGroup in weeklyShifts.GroupBy(ByStaffId))
            {
                bool IsForCurrentStaffGroup(RosterShift shift) => shift.StaffId == staffShiftGroup.Key;
                int ShiftIdSelector(RosterShift shift) => shift.Id;
                double WeekOverlapHours(RosterShift shift) =>
                    GetOverlapHours(shift.Start, shift.End, normalizedWeekStart, normalizedWeekEnd);

                var staffWeeklyShifts = staffShiftGroup.OrderBy(ByStart).ToList();
                var staffAllShifts = allShifts
                    .Where(IsForCurrentStaffGroup)
                    .OrderBy(ByStart)
                    .ToList();
                var weeklyShiftIds = staffWeeklyShifts.Select(ShiftIdSelector).ToHashSet();

                var totalHours = staffWeeklyShifts.Sum(WeekOverlapHours);

                if (totalHours > MaxWeeklyHours)
                {
                    foreach (var shift in staffWeeklyShifts)
                    {
                        violations.Add(new AuditViolation
                        {
                            ShiftId = shift.Id,
                            StaffId = shift.StaffId,
                            StaffName = shift.StaffName,
                            ShiftStart = shift.Start,
                            ShiftEnd = shift.End,
                            Rule = MaxWeeklyHoursViolationRule,
                            Message = $"Weekly total is {totalHours:F1}h (limit {MaxWeeklyHours:F0}h).",
                        });
                    }
                }

                for (int shiftIndex = 1; shiftIndex < staffAllShifts.Count; shiftIndex++)
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
                            Rule = MinRestViolationRule,
                            Message = $"Rest gap is {restGap.TotalHours:F1}h (minimum {MinRestGap.TotalHours:F0}h).",
                        });
                    }
                }
            }

            string ByShiftIdAndRule(AuditViolation violation) => $"{violation.ShiftId}:{violation.Rule}";
            AuditViolation FirstInViolationGroup(IGrouping<string, AuditViolation> violationGroup) => violationGroup.First();
            DateTime ByShiftStart(AuditViolation violation) => violation.ShiftStart;

            var dedupedViolations = violations
                .GroupBy(ByShiftIdAndRule)
                .Select(FirstInViolationGroup)
                .OrderBy(ByShiftStart)
                .ToList();

            var suggestions = BuildSuggestions(dedupedViolations, normalizedWeekStart, weeklyShifts, allShifts, eligibleStaffProfiles);

            return new AutoAuditResult
            {
                WeekStart = normalizedWeekStart,
                HasConflicts = dedupedViolations.Count > 0,
                Summary = dedupedViolations.Count == 0
                    ? "No conflicts found. Roster can be published."
                    : $"Found {dedupedViolations.Count} conflict(s). Publishing is blocked until resolved.",
                Violations = dedupedViolations,
                Suggestions = suggestions,
            };
        }

        private Dictionary<int, StaffProfile> BuildStaffProfilesById()
        {
            var profilesById = new Dictionary<int, StaffProfile>();
            foreach (var staffMember in staffRepository.LoadAllStaff())
            {
                profilesById[staffMember.StaffID] = new StaffProfile
                {
                    StaffId = staffMember.StaffID,
                    FullName = ($"{staffMember.FirstName} {staffMember.LastName}").Trim(),
                    Role = staffMember switch
                    {
                        Doctor => DoctorRole,
                        Pharmacyst => PharmacistRole,
                        _ => string.Empty,
                    },
                    Specialization = staffMember switch
                    {
                        Doctor doctor when !string.IsNullOrWhiteSpace(doctor.Specialization) => doctor.Specialization,
                        Pharmacyst pharmacist when !string.IsNullOrWhiteSpace(pharmacist.Certification) => pharmacist.Certification,
                        _ => DefaultSpecialization,
                    },
                    IsAvailable = staffMember.Available,
                    IsActive = true,
                    Status = staffMember is Doctor doctorMember ? doctorMember.DoctorStatus.ToString() : AvailableStaffStatus,
                };
            }
            return profilesById;
        }

        private IReadOnlyList<RosterShift> BuildAllRosterShifts(IReadOnlyDictionary<int, StaffProfile> staffProfilesById)
        {
            var rosterShifts = new List<RosterShift>();
            foreach (var shift in shiftRepository.GetAllShifts())
            {
                int staffId = shift.AppointedStaff.StaffID;
                StaffProfile? matchingProfile = staffProfilesById.TryGetValue(staffId, out var profile) ? profile : null;
                rosterShifts.Add(new RosterShift
                {
                    Id = shift.Id,
                    StaffId = staffId,
                    StaffName = matchingProfile?.FullName ?? string.Empty,
                    Role = matchingProfile?.Role ?? string.Empty,
                    Specialization = matchingProfile?.Specialization ?? DefaultSpecialization,
                    Start = shift.StartTime,
                    End = shift.EndTime,
                    Status = shift.Status.ToString(),
                });
            }
            return rosterShifts;
        }

        private List<AutoSuggestRecommendation> BuildSuggestions(
            IReadOnlyList<AuditViolation> violations,
            DateTime weekStart,
            IReadOnlyList<RosterShift> weeklyShifts,
            IReadOnlyList<RosterShift> allShifts,
            IReadOnlyList<StaffProfile> staffProfiles)
        {
            int RosterShiftKey(RosterShift rosterShift) => rosterShift.Id;
            RosterShift IdentityRosterShift(RosterShift rosterShift) => rosterShift;

            var weeklyShiftsById = weeklyShifts.ToDictionary(RosterShiftKey, IdentityRosterShift);
            var effectiveShifts = allShifts.Select(CloneShift).ToList();
            var recommendations = new List<AutoSuggestRecommendation>();

            foreach (var violation in violations)
            {
                if (!weeklyShiftsById.TryGetValue(violation.ShiftId, out var violatingShift))
                {
                    continue;
                }

                bool MatchesViolatingShiftId(RosterShift existingShift) => existingShift.Id == violatingShift.Id;
                var violatingShiftInPlan = effectiveShifts.FirstOrDefault(MatchesViolatingShiftId) ?? violatingShift;

                bool IsNotCurrentStaff(StaffProfile staffProfile) => staffProfile.StaffId != violatingShiftInPlan.StaffId;
                bool HasMatchingRole(StaffProfile staffProfile) => string.Equals(staffProfile.Role, violatingShiftInPlan.Role, StringComparison.OrdinalIgnoreCase);
                bool HasMatchingSpecialization(StaffProfile staffProfile) => string.Equals(staffProfile.Specialization, violatingShiftInPlan.Specialization, StringComparison.OrdinalIgnoreCase);
                bool CanTakeViolatingShift(StaffProfile staffProfile) => CanTakeShift(staffProfile.StaffId, violatingShiftInPlan, effectiveShifts, weekStart);
                double GetMonthlyHours(StaffProfile staffProfile) => GetMonthlyWorkedHoursFromShifts(staffProfile.StaffId, violatingShiftInPlan.Start.Year, violatingShiftInPlan.Start.Month, effectiveShifts);
                string ByCandidateName(StaffProfile staffProfile) => staffProfile.FullName;

                var matchingSpecializationCandidates = staffProfiles
                    .Where(IsNotCurrentStaff)
                    .Where(HasMatchingRole)
                    .Where(HasMatchingSpecialization)
                    .Where(IsAvailableForReassignment)
                    .Where(CanTakeViolatingShift)
                    .OrderBy(GetMonthlyHours)
                    .ThenBy(ByCandidateName)
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
                        .ThenBy(ByCandidateName)
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
                        Reason = "No valid replacement found for this role under overlap/rest/hour limits.",
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
                        : $"Lowest monthly load in matching specialization pool ({monthlyHours:F1}h).",
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
            DateTime ByStart(RosterShift shift) => shift.Start;

            var candidateShifts = allShifts
                .Where(IsForCandidate)
                .OrderBy(ByStart)
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

            var weekEnd = weekStart.AddDays(DaysInWeek);
            double WeekOverlapHours(RosterShift shift) => GetOverlapHours(shift.Start, shift.End, weekStart, weekEnd);
            var existingHours = candidateShifts.Sum(WeekOverlapHours);
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

        private static RosterShift CloneShift(RosterShift source) => new RosterShift
        {
            Id = source.Id,
            StaffId = source.StaffId,
            StaffName = source.StaffName,
            Role = source.Role,
            Specialization = source.Specialization,
            Start = source.Start,
            End = source.End,
            Status = source.Status,
        };

        private static bool IsAuditableShift(RosterShift shift) =>
            !string.Equals(shift.Status, CancelledShiftStatus, StringComparison.OrdinalIgnoreCase);

        private static bool IsEligibleStaff(StaffProfile staff)
        {
            var isInactiveByStatus = string.Equals(staff.Status, InactiveStaffStatus, StringComparison.OrdinalIgnoreCase);
            return staff.IsActive != false && !isInactiveByStatus;
        }

        private static bool IsAvailableForReassignment(StaffProfile staff) => staff.IsAvailable != false;

        private static bool OverlapsWindow(RosterShift shift, DateTime windowStart, DateTime windowEnd) =>
            shift.Start < windowEnd && shift.End > windowStart;

        private static double GetMonthlyWorkedHoursFromShifts(int staffId, int year, int month, IReadOnlyList<RosterShift> shifts)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1);

            bool IsForStaff(RosterShift shift) => shift.StaffId == staffId;
            double MonthOverlapHours(RosterShift shift) => GetOverlapHours(shift.Start, shift.End, monthStart, monthEnd);

            return shifts
                .Where(IsForStaff)
                .Sum(MonthOverlapHours);
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
                shift.StaffId == candidateStaffId
                && shift.Id != proposed.Id
                && shift.Start < proposed.End
                && shift.End > proposed.Start;

            return allShifts.Any(OverlapsWithCandidate);
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var daysFromMonday = (DaysInWeek + (date.DayOfWeek - DayOfWeek.Monday)) % DaysInWeek;
            return date.Date.AddDays(-daysFromMonday);
        }
    }
}
