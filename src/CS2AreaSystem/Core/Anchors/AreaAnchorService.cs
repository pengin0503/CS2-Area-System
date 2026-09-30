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
    public sealed class AreaAnchorService
    {
        private readonly IAreaAnchorStore store;
        private readonly Func<Entity, bool> valid;
        private readonly Func<Entity, Entity, AreaAnchorRole, bool> eligible;
        private readonly Dictionary<Entity, HashSet<Entity>> byAnchor = new Dictionary<Entity, HashSet<Entity>>();
        private readonly HashSet<Entity> dirty = new HashSet<Entity>();
        public AreaAnchorService(IAreaAnchorStore store, Func<Entity, bool> valid, Func<Entity, Entity, AreaAnchorRole, bool> eligible)
        { this.store = store; this.valid = valid; this.eligible = eligible; }
        public bool Assign(Entity area, Entity anchor, AreaAnchorRole role)
        {
            if (!valid(area) || !valid(anchor) || !eligible(area, anchor, role)) return false;
            var old = store.Read(area);
            var links = old.Where(link => !(link.Role == role && (role == AreaAnchorRole.Headquarters || link.Anchor == anchor))).ToList();
            links.Add(new AreaAnchorLink { Anchor = anchor, Role = role });
            store.Write(area, links); Reindex(area, old, links); dirty.Add(area); return true;
        }
        public bool Remove(Entity area, Entity anchor, AreaAnchorRole role)
        {
            var old = store.Read(area);
            var links = old.Where(link => link.Anchor != anchor || link.Role != role).ToArray();
            if (links.Length == old.Count) return false;
            store.Write(area, links); Reindex(area, old, links); dirty.Add(area); return true;
        }
        public bool HasValidAnchor(Entity area, AreaAnchorRole role) => TryGetValidAnchor(area, role, out _);
        // First valid anchor of the role; kept for single-anchor roles and existing callers.
        public bool TryGetValidAnchor(Entity area, AreaAnchorRole role, out Entity anchor)
        {
            var anchors = GetValidAnchors(area, role);
            anchor = anchors.Count > 0 ? anchors[0] : Entity.Null;
            return anchors.Count > 0;
        }
        // Every persisted link of the role that is still valid and eligible, in link order without duplicates.
        public IReadOnlyList<Entity> GetValidAnchors(Entity area, AreaAnchorRole role)
        {
            var result = new List<Entity>();
            foreach (var link in store.Read(area))
                if (link.Role == role && !result.Contains(link.Anchor) && valid(link.Anchor) && eligible(area, link.Anchor, role)) result.Add(link.Anchor);
            return result;
        }
        public void RebuildReverseIndex(IEnumerable<Entity> areas)
        {
            byAnchor.Clear(); dirty.Clear();
            foreach (var area in areas) Reindex(area, Array.Empty<AreaAnchorLink>(), store.Read(area));
        }
        public void InvalidateAnchor(Entity anchor)
        { if (byAnchor.TryGetValue(anchor, out var areas)) dirty.UnionWith(areas); }
        public void InvalidateArea(Entity area) => dirty.Add(area);
        public void ForgetArea(Entity area)
        {
            var old = store.Read(area);
            Reindex(area, old, Array.Empty<AreaAnchorLink>());
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
