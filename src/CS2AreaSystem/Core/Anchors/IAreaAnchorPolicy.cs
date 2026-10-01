using System;
using Unity.Entities;
namespace CS2AreaSystem.Core.Anchors
{
    // Module-specific anchor rules. Core enforces generic validity/membership first; a module then applies its
    // own stricter predicate (for example, Park rejects entrances that are not plausible leisure facilities).
    public interface IAreaAnchorPolicy
    {
        AreaAnchorRole? RequiredAnchorRole { get; }
        bool AcceptsAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities);
    }
    // Optional: a module whose assignment rule must not be re-applied to an already accepted link (for example,
    // Industrial keeps an HQ through tenant vacancy). Core still checks validity, role and membership first.
    // Modules that do not implement it validate existing links with AcceptsAnchor.
    public interface IAreaExistingAnchorPolicy
    {
        bool AcceptsExistingAnchor(Entity area, Entity anchor, AreaAnchorRole role, EntityManager entities);
    }
    public static class AreaAnchorEligibility
    {
        // Order: valid area -> building anchor -> module/role -> current membership -> module predicate.
        // Later checks never run when an earlier one fails, so modules only see current member buildings.
        public static bool Evaluate(Func<bool> validArea, Func<bool> isBuilding, Func<bool> moduleRole, Func<bool> isMember, Func<bool> moduleAccepts) =>
            validArea() && isBuilding() && moduleRole() && isMember() && moduleAccepts();
    }
}
