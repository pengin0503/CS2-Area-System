using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Membership;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // Experienced state of a visit, each component 0–100 (design §9.1).
    public struct ParkVisitExperience
    {
        public float Facility, Landscape, Comfort, Maintenance, ValueForMoney, Accessibility;
    }
    public enum ParkVisitEventKind : byte { Started, Completed }
    public struct ParkVisitEvent
    {
        public ParkVisitEventKind Kind;
        public Entity Citizen;
        public ulong ParkId;
        public bool Tourist;
        public bool FeeSatisfied;
        public float Satisfaction;
    }
    public static class ParkVisitModel
    {
        public const float FacilityWeight = 0.30f, LandscapeWeight = 0.20f, ComfortWeight = 0.20f, MaintenanceWeight = 0.15f, ValueWeight = 0.10f, AccessWeight = 0.05f;
        public const float NeutralFacilityExperience = 50f;
        // Leisure efficiency at which the facility experience reaches ~63% of its maximum.
        public const float FacilityEfficiencyScale = 25f;
        private static float Clamp(float v) => float.IsNaN(v) ? 0f : v < 0 ? 0 : v > 100 ? 100 : v;
        public static float ComputeVisitSample(in ParkVisitExperience e) =>
            Clamp(FacilityWeight * Clamp(e.Facility) + LandscapeWeight * Clamp(e.Landscape) + ComfortWeight * Clamp(e.Comfort) +
                  MaintenanceWeight * Clamp(e.Maintenance) + ValueWeight * Clamp(e.ValueForMoney) + AccessWeight * Clamp(e.Accessibility));
        // Actual visited provider data first (LeisureProviderData.m_Efficiency); attractions without it stay neutral.
        public static float FacilityExperience(bool hasLeisureData, int efficiency) =>
            hasLeisureData ? Clamp(100f * (1f - (float)Math.Exp(-Math.Max(0, efficiency) / FacilityEfficiencyScale))) : NeutralFacilityExperience;
        public static ParkAreaState RecordCompletedVisit(ParkAreaState state, bool tourist, float satisfaction)
        {
            state.LifetimeVisitors++;
            state.Satisfaction = state.Satisfaction.Add(satisfaction);
            if (tourist) { state.LifetimeTouristVisitors++; state.TouristSatisfaction = state.TouristSatisfaction.Add(satisfaction); }
            else { state.LifetimeResidentVisitors++; state.ResidentSatisfaction = state.ResidentSatisfaction.Add(satisfaction); }
            return state;
        }
        // Core's deterministic same-kind primary owns exclusive visitor metrics. If that Park is not Active the visit
        // belongs to nobody rather than falling through to an overlapping Park (no double counting, no ownership hopping).
        public static ulong PrimaryPark(IReadOnlyList<AreaMembership> relations, int parkTypeId, Func<ulong, bool> isActive)
        {
            foreach (var relation in relations)
                if (relation.TypeId == parkTypeId && relation.IsPrimaryForType) return isActive(relation.AreaStableId) ? relation.AreaStableId : 0;
            return 0;
        }
    }
    // Runtime-only visit sessions (never serialized). One session per citizen; a session belongs to one Park.
    public sealed class ParkVisitTracker
    {
        private sealed class Session
        {
            public ulong ParkId;
            public bool Tourist, FeeSatisfied;
            public uint LastSeen;
            public double ExperienceSum;
            public int ExperienceCount;
        }
        private readonly Dictionary<Entity, Session> sessions = new Dictionary<Entity, Session>();
        private readonly Dictionary<ulong, int> active = new Dictionary<ulong, int>();
        private readonly List<Entity> expired = new List<Entity>();
        private readonly uint graceFrames;
        private uint seedUntil;
        private bool seeding;
        public ParkVisitTracker(uint graceFrames) { this.graceFrames = graceFrames; }
        public int SessionCount => sessions.Count;
        public int ActiveCount(ulong parkId) => active.TryGetValue(parkId, out var count) ? count : 0;
        public IReadOnlyDictionary<ulong, int> ActiveByPark => active;
        // Visits observed before this frame are restored in-progress visits after a load: they never pay again.
        public void SeedPreExistingUntil(uint frame) { seedUntil = frame; seeding = true; }
        public void Clear() { sessions.Clear(); active.Clear(); seeding = false; }
        public void Observe(Entity citizen, ulong parkId, bool tourist, float experience, uint frame, List<ParkVisitEvent> events)
        {
            sessions.TryGetValue(citizen, out var session);
            if (parkId == 0) return; // not at a relevant building of an Active Park: an existing session only ages
            if (session != null && session.ParkId != parkId) { Complete(citizen, session, events); sessions.Remove(citizen); session = null; }
            if (session == null)
            {
                session = new Session { ParkId = parkId, Tourist = tourist, FeeSatisfied = seeding && frame < seedUntil };
                sessions.Add(citizen, session);
                active[parkId] = ActiveCount(parkId) + 1;
                events.Add(new ParkVisitEvent { Kind = ParkVisitEventKind.Started, Citizen = citizen, ParkId = parkId, Tourist = tourist, FeeSatisfied = session.FeeSatisfied });
            }
            session.Tourist = tourist;
            session.LastSeen = frame;
            if (!float.IsNaN(experience)) { session.ExperienceSum += experience; session.ExperienceCount++; }
        }
        // Completes sessions not re-observed within the grace period (left the Park or stopped leisure).
        public void Expire(uint frame, List<ParkVisitEvent> events)
        {
            expired.Clear();
            foreach (var pair in sessions)
                if (frame > pair.Value.LastSeen && frame - pair.Value.LastSeen > graceFrames) expired.Add(pair.Key);
            foreach (var citizen in expired) { Complete(citizen, sessions[citizen], events); sessions.Remove(citizen); }
        }
        // Admission is attempted at most once per session, whatever the outcome of the transaction.
        public bool TryBeginAdmission(Entity citizen, ulong parkId)
        {
            if (!sessions.TryGetValue(citizen, out var session) || session.ParkId != parkId || session.FeeSatisfied) return false;
            session.FeeSatisfied = true;
            return true;
        }
        private void Complete(Entity citizen, Session session, List<ParkVisitEvent> events)
        {
            int count = ActiveCount(session.ParkId) - 1;
            if (count > 0) active[session.ParkId] = count; else active.Remove(session.ParkId);
            float satisfaction = session.ExperienceCount > 0 ? (float)(session.ExperienceSum / session.ExperienceCount) : ParkSatisfactionState.Prior;
            events.Add(new ParkVisitEvent { Kind = ParkVisitEventKind.Completed, Citizen = citizen, ParkId = session.ParkId, Tourist = session.Tourist,
                FeeSatisfied = session.FeeSatisfied, Satisfaction = satisfaction });
        }
    }
}
namespace CS2AreaSystem.Modules.Park
{
    // Observation cadence mirrors vanilla LeisureSystem: 4096 updates/day, 16 staggered citizen groups.
    public static class ParkVisitCadence
    {
        public const int Groups = 16;
        public const uint UpdateInterval = 262144 / 4096;
        public const uint CycleFrames = UpdateInterval * Groups;
        // A session survives two missed observations (short transitions between buildings or paths).
        public const uint GraceFrames = 2 * CycleFrames + UpdateInterval;
        // After a load, visitors first seen within one full cycle (+1 update) are restored, fee-satisfied visits.
        public const uint SeedFrames = CycleFrames + UpdateInterval;
        public static uint GroupFor(uint frame) => (frame / UpdateInterval) & (Groups - 1);
    }
    public static class ParkStateDefaults
    {
        public static Core.Areas.ManagedAreaProgress Progress() =>
            new Core.Areas.ManagedAreaProgress { SchemaVersion = Core.Areas.ManagedAreaProgress.CurrentSchemaVersion, Level = ParkProgressionModel.MinLevel };
        public static Core.Areas.ManagedAreaSettings Settings() =>
            new Core.Areas.ManagedAreaSettings { SchemaVersion = Core.Areas.ManagedAreaSettings.CurrentSchemaVersion, BudgetMultiplier = ParkFinanceModel.DefaultBudget };
    }
    public static class ParkVisitRecorder
    {
        public static void RecordRecent(ParkRuntimeSnapshot snapshot, bool tourist, uint frame)
        {
            snapshot.RecentVisitors.Add(frame, 1);
            if (tourist) snapshot.RecentTouristVisitors.Add(frame, 1);
        }
        public static void RecordOccupancy(ParkRuntimeSnapshot snapshot, int occupancy)
        { if (occupancy > snapshot.PeakOccupancy) snapshot.PeakOccupancy = occupancy; }
    }
}
