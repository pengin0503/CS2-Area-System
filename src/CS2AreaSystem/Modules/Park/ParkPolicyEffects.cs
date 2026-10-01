using System;
using CS2AreaSystem.Core.Policies;
using Unity.Entities;
namespace CS2AreaSystem.Modules.Park
{
    // Which Park policies are in effect for one Area, and the model inputs they change (design §9.1, §11). Every effect is
    // Area-internal: it changes a Park model input, never Quality after the six-metric model. The default value is the
    // neutral baseline (no policy). Night Events, Quiet Operations and Major Event Attraction have no verified 1.6.2f1
    // path (docs/verification/park-policy-effect-contracts.md), so they are never read here.
    public readonly struct ParkPolicyEffects : IEquatable<ParkPolicyEffects>
    {
        // Enhanced Maintenance: Park management is funded 25% above the budget slider; coverage is that funding.
        public const double EnhancedMaintenanceFundingMultiplier = 1.25;
        // Landscape Protection: vegetation and decoration count 25% more toward Landscape; 15% of comfortable capacity is
        // closed to visitors.
        public const float LandscapeProtectionScenicCredit = 1.25f, LandscapeProtectionCapacityShare = 0.85f;
        // Premium Services: the admission fee visitors consider fair is 30% higher.
        public const double PremiumServicesFairFeeMultiplier = 1.3;
        public static readonly ParkPolicyEffects None = default;
        public readonly bool EnhancedMaintenance, LandscapeProtection, PremiumServices;
        public ParkPolicyEffects(bool enhancedMaintenance, bool landscapeProtection, bool premiumServices)
        { EnhancedMaintenance = enhancedMaintenance; LandscapeProtection = landscapeProtection; PremiumServices = premiumServices; }
        public static ParkPolicyEffects From(Func<int, bool> isEffective) => new ParkPolicyEffects(
            isEffective(AreaPolicyIds.EnhancedMaintenance), isEffective(AreaPolicyIds.LandscapeProtection), isEffective(AreaPolicyIds.PremiumServices));
        public double ManagementFundingMultiplier => EnhancedMaintenance ? EnhancedMaintenanceFundingMultiplier : 1.0;
        public float ScenicCredit => LandscapeProtection ? LandscapeProtectionScenicCredit : 1f;
        public float CapacityShare => LandscapeProtection ? LandscapeProtectionCapacityShare : 1f;
        public double FairFeeMultiplier => PremiumServices ? PremiumServicesFairFeeMultiplier : 1.0;
        public bool Equals(ParkPolicyEffects other) =>
            EnhancedMaintenance == other.EnhancedMaintenance && LandscapeProtection == other.LandscapeProtection && PremiumServices == other.PremiumServices;
        public override bool Equals(object obj) => obj is ParkPolicyEffects other && Equals(other);
        public override int GetHashCode() => (EnhancedMaintenance ? 1 : 0) | (LandscapeProtection ? 2 : 0) | (PremiumServices ? 4 : 0);
        public override string ToString() => $"EnhancedMaintenance={EnhancedMaintenance}, LandscapeProtection={LandscapeProtection}, PremiumServices={PremiumServices}";
    }
    // Reads one Area's Park policy effects for a Park system. A failing policy read keeps the Park on the neutral baseline
    // (failure isolation) and is reported once.
    public sealed class ParkPolicyEffectReader
    {
        private readonly Func<Entity, int, bool> isEffective;
        private readonly Action<Exception> report;
        private bool reported;
        public ParkPolicyEffectReader(Func<Entity, int, bool> isEffective, Action<Exception> report)
        { this.isEffective = isEffective; this.report = report; }
        public ParkPolicyEffects Read(Entity area)
        {
            try { return ParkPolicyEffects.From(id => isEffective(area, id)); }
            catch (Exception error)
            {
                if (!reported) { reported = true; report?.Invoke(error); }
                return ParkPolicyEffects.None;
            }
        }
    }
}
