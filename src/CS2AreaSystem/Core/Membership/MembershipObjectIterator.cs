using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Core.Membership
{
    public interface IMembershipObjectFilter
    {
        bool Accept(Entity item);
    }
    // Static-object search over an Area's bounds that keeps only possible members (#51): Game.Objects.SearchSystem's static
    // tree holds every static object (trees, props, ...), and in 1.6.2f1 there is no building-only tree, so the filter runs
    // inside the iteration instead of collecting every object first and rejecting non-buildings later.
    public struct MembershipObjectIterator<TFilter> : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        where TFilter : struct, IMembershipObjectFilter
    {
        public Bounds2 Bounds;
        public HashSet<Entity> Candidates;
        public TFilter Filter;
        public int Visited, Accepted;
        public bool Intersect(QuadTreeBoundsXZ bounds) => math.all(Bounds.min <= bounds.m_Bounds.xz.max) && math.all(Bounds.max >= bounds.m_Bounds.xz.min);
        public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
        {
            if (!Intersect(bounds)) return;
            Visited++;
            if (!Filter.Accept(item) || !Candidates.Add(item)) return;
            Accepted++;
        }
    }
    // Main-thread read-only lookups, refreshed by AreaMembershipSystem before each search. The full membership gate
    // (Transform, Static, Deleted, Temp, Overridden) still runs in FindContainingAreas.
    public struct BuildingMemberFilter : IMembershipObjectFilter
    {
        public ComponentLookup<Game.Buildings.Building> Buildings;
        public ComponentLookup<Deleted> Deleted;
        public bool Accept(Entity item) => Buildings.HasComponent(item) && !Deleted.HasComponent(item);
    }
}
