using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
namespace CS2AreaSystem.Core.Anchors
{
    public interface IAreaAnchorStore
    {
        IReadOnlyList<AreaAnchorLink> Read(Entity area);
        void Write(Entity area, IReadOnlyList<AreaAnchorLink> links);
    }
    // One persisted link as the UI sees it: Exists = the entity is still a valid game object,
    // Valid = it also passes the Core + module eligibility (an invalid link no longer activates the area).
    public readonly struct AreaAnchorLinkState
    {
        public Entity Anchor { get; }
        public bool Exists { get; }
        public bool Valid { get; }
        public AreaAnchorLinkState(Entity anchor, bool exists, bool valid) { Anchor = anchor; Exists = exists; Valid = valid; }
    }
    public sealed class AreaAnchorService
    {
        private readonly IAreaAnchorStore store;
        private readonly Func<Entity, bool> valid;
        private readonly Func<Entity, Entity, AreaAnchorRole, bool> eligible;
        // Validation of persisted links; defaults to the assignment predicate.
        private readonly Func<Entity, Entity, AreaAnchorRole, bool> existingEligible;
        private readonly Dictionary<Entity, HashSet<Entity>> byAnchor = new Dictionary<Entity, HashSet<Entity>>();
        private readonly HashSet<Entity> dirty = new HashSet<Entity>();
        // Advances whenever links change or an Area's anchors may have changed validity, for UI caches (#70).
        public long Revision { get; private set; }
        public AreaAnchorService(IAreaAnchorStore store, Func<Entity, bool> valid, Func<Entity, Entity, AreaAnchorRole, bool> eligible,
            Func<Entity, Entity, AreaAnchorRole, bool> existingEligible = null)
        { this.store = store; this.valid = valid; this.eligible = eligible; this.existingEligible = existingEligible ?? eligible; }
        public bool Assign(Entity area, Entity anchor, AreaAnchorRole role)
        {
            if (!valid(area) || !valid(anchor) || !eligible(area, anchor, role)) return false;
            var old = store.Read(area);
            var links = old.Where(link => !(link.Role == role && (role == AreaAnchorRole.Headquarters || link.Anchor == anchor))).ToList();
            links.Add(new AreaAnchorLink { Anchor = anchor, Role = role });
            store.Write(area, links); Reindex(area, old, links); dirty.Add(area); Revision++; return true;
        }
        public bool Remove(Entity area, Entity anchor, AreaAnchorRole role)
        {
            var old = store.Read(area);
            var links = old.Where(link => link.Anchor != anchor || link.Role != role).ToArray();
            if (links.Length == old.Count) return false;
            store.Write(area, links); Reindex(area, old, links); dirty.Add(area); Revision++; return true;
        }
        public bool HasValidAnchor(Entity area, AreaAnchorRole role) => TryGetValidAnchor(area, role, out _);
        // First valid anchor of the role; kept for single-anchor roles and existing callers.
        public bool TryGetValidAnchor(Entity area, AreaAnchorRole role, out Entity anchor)
        {
            var anchors = GetValidAnchors(area, role);
            anchor = anchors.Count > 0 ? anchors[0] : Entity.Null;
            return anchors.Count > 0;
        }
        // Every persisted link of the role that is still valid and still accepted, in link order without duplicates.
        public IReadOnlyList<Entity> GetValidAnchors(Entity area, AreaAnchorRole role)
        {
            var result = new List<Entity>();
            foreach (var link in store.Read(area))
                if (link.Role == role && !result.Contains(link.Anchor) && valid(link.Anchor) && existingEligible(area, link.Anchor, role)) result.Add(link.Anchor);
            return result;
        }
        // Every persisted link of the role (valid or not) in link order without duplicates, so stale links stay
        // visible and removable instead of silently blocking activation.
        public IReadOnlyList<AreaAnchorLinkState> GetAnchorLinks(Entity area, AreaAnchorRole role)
        {
            var result = new List<AreaAnchorLinkState>();
            var seen = new HashSet<Entity>();
            foreach (var link in store.Read(area))
            {
                if (link.Role != role || !seen.Add(link.Anchor)) continue;
                bool exists = valid(link.Anchor);
                result.Add(new AreaAnchorLinkState(link.Anchor, exists, exists && existingEligible(area, link.Anchor, role)));
            }
            return result;
        }
        public bool HasLinks(Entity area, AreaAnchorRole role)
        {
            foreach (var link in store.Read(area)) if (link.Role == role) return true;
            return false;
        }
        public void RebuildReverseIndex(IEnumerable<Entity> areas)
        {
            byAnchor.Clear(); dirty.Clear(); Revision++;
            foreach (var area in areas) Reindex(area, Array.Empty<AreaAnchorLink>(), store.Read(area));
        }
        public void InvalidateAnchor(Entity anchor)
        { if (byAnchor.TryGetValue(anchor, out var areas)) { dirty.UnionWith(areas); Revision++; } }
        public void InvalidateArea(Entity area) { dirty.Add(area); Revision++; }
        public void ForgetArea(Entity area)
        {
            var old = store.Read(area);
            Reindex(area, old, Array.Empty<AreaAnchorLink>());
            Revision++;
            // Keep one final validation queued so module deletion hooks/state transitions can run.
            dirty.Add(area);
        }
        public Entity[] DrainDirty() { var result = dirty.ToArray(); dirty.Clear(); return result; }
        private void Reindex(Entity area, IReadOnlyList<AreaAnchorLink> old, IReadOnlyList<AreaAnchorLink> links)
        {
            foreach (var link in old) if (byAnchor.TryGetValue(link.Anchor, out var areas)) { areas.Remove(area); if (areas.Count == 0) byAnchor.Remove(link.Anchor); }
            foreach (var link in links)
            {
                if (!valid(link.Anchor)) continue;
                if (!byAnchor.TryGetValue(link.Anchor, out var areas)) byAnchor[link.Anchor] = areas = new HashSet<Entity>();
                areas.Add(area);
            }
        }
    }
    public sealed class EntityAreaAnchorStore : IAreaAnchorStore
    {
        private readonly EntityManager entities;
        public EntityAreaAnchorStore(EntityManager entities) => this.entities = entities;
        public IReadOnlyList<AreaAnchorLink> Read(Entity area)
        {
            if (!entities.Exists(area) || !entities.HasBuffer<AreaAnchorLink>(area)) return Array.Empty<AreaAnchorLink>();
            var buffer = entities.GetBuffer<AreaAnchorLink>(area, true);
            var copy = new AreaAnchorLink[buffer.Length]; for (int i = 0; i < copy.Length; i++) copy[i] = buffer[i]; return copy;
        }
        public void Write(Entity area, IReadOnlyList<AreaAnchorLink> links)
        {
            var buffer = entities.HasBuffer<AreaAnchorLink>(area) ? entities.GetBuffer<AreaAnchorLink>(area) : entities.AddBuffer<AreaAnchorLink>(area);
            buffer.Clear(); foreach (var link in links) buffer.Add(link);
        }
    }
}
