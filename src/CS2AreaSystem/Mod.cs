using Game;
using CS2AreaSystem.Core.Membership;
using Game.Modding;
using Colossal.Logging;
using Colossal.Localization;
using CS2AreaSystem.Core.Prefabs;
using CS2AreaSystem.Core.UI;
using Game.Prefabs;
using CS2AreaSystem.Core.Areas;
namespace CS2AreaSystem
{
    public sealed class Mod : IMod
    {
        // ModManager creates IMod instances with FormatterServices.GetUninitializedObject:
        // constructors and instance field initializers never run, so state is created in OnLoad.
        private LocalizationSourceRegistration<MemorySource> locales;
        private CS2AreaSystem.Core.Settings.AreaSystemSettings settings;
        internal static readonly ILog Log = LogManager.GetLogger(nameof(CS2AreaSystem));
        public void OnLoad(UpdateSystem updateSystem)
        {
            CS2AreaSystem.Core.Persistence.ComponentSchema.ReportUnsupported = message => Log.Error(message);
            updateSystem.UpdateBefore<AreaPrefabRegistrationSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            // ToolbarUISystem rebuilds its bindings only in a frame that holds an Unlock event (see AreaToolbarUnlockSystem).
            updateSystem.UpdateBefore<AreaToolbarUnlockSystem, Game.UI.InGame.ToolbarUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateBefore<ManagedAreaOverlapSystem, Game.Notifications.IconCommandSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAt<ManagedAreaToolGuardSystem>(SystemUpdatePhase.PreTool);
            updateSystem.UpdateAt<ManagedAreaLifecycleSystem>(SystemUpdatePhase.ModificationEnd);
            try { updateSystem.UpdateAfter<CS2AreaSystem.Core.Labels.AreaPlacementSystem, ManagedAreaLifecycleSystem>(SystemUpdatePhase.ModificationEnd); }
            catch (System.Exception error) { Log.Error(error, "Area naming after placement unavailable; Areas keep their default names."); }
            updateSystem.UpdateAfter<CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<CS2AreaSystem.Core.Policies.AreaPolicyBootstrapSystem, CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<AreaMembershipSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<CS2AreaSystem.Core.Anchors.AreaActivationSystem>(SystemUpdatePhase.ModificationEnd);
            // Entrance evidence is an activation requirement, independently of finance/gameplay registration.
            try { updateSystem.UpdateBefore<CS2AreaSystem.Modules.Park.ParkEntranceSystem, CS2AreaSystem.Core.Anchors.AreaActivationSystem>(SystemUpdatePhase.ModificationEnd); }
            catch (System.Exception error) { Log.Error(error, "Park entrance detection unavailable; no derived entrance is assumed."); }
            try { updateSystem.UpdateAfter<CS2AreaSystem.Modules.Park.ParkConnectivitySystem, CS2AreaSystem.Modules.Park.ParkEntranceSystem>(SystemUpdatePhase.ModificationEnd); }
            catch (System.Exception error) { Log.Error(error, "Park connectivity diagnostics unavailable; no pathfinding is changed."); }
            try { updateSystem.UpdateAfter<CS2AreaSystem.Core.Policies.AreaPolicySystem, CS2AreaSystem.Core.Anchors.AreaActivationSystem>(SystemUpdatePhase.ModificationEnd); }
            catch (System.Exception error) { Log.Error(error, "Area policy runtime unavailable; Area Core and modules remain registered."); }
            var finance = RegisterAreaFinance(updateSystem);
            var park = RegisterParkSystems(updateSystem, finance);
            var industrial = RegisterIndustrialSystems(updateSystem);
            RegisterIndustrialSupport(updateSystem, finance, industrial);
            RegisterParkEvents(updateSystem, finance, park);
            RegisterParkCoverageModifier(updateSystem, park);
            RegisterLocalization();
            RegisterSettings();
            // After DefaultToolSystem in the same frame, so an accepted label click replaces the selection vanilla just made.
            try { updateSystem.UpdateAfter<CS2AreaSystem.Core.Labels.AreaLabelSystem, Game.Tools.DefaultToolSystem>(SystemUpdatePhase.ToolUpdate); }
            catch (System.Exception error) { Log.Error(error, "Area map labels unavailable; Areas stay selectable from the Area list."); }
            try { updateSystem.UpdateAfter<AreaManagementUISystem>(SystemUpdatePhase.UIUpdate); }
            catch (System.Exception error) { Log.Error(error, "Area management UI registration failed; simulation remains registered."); }
            try { updateSystem.UpdateAfter<CS2AreaSystem.Core.Finance.AreaBudgetBindingSystem>(SystemUpdatePhase.UIUpdate); }
            catch (System.Exception error) { Log.Error(error, "Area budget rows unavailable; the vanilla budget and Area finance are unaffected."); }
        }
        public void OnDispose()
        {
            CS2AreaSystem.Core.Persistence.ComponentSchema.ReportUnsupported = null;
            CS2AreaSystem.Core.Settings.AreaSettings.Use(null);
            var registeredSettings = settings;
            settings = null;
            if (registeredSettings != null)
            {
                try { registeredSettings.UnregisterInOptionsUI(); }
                catch (System.Exception error) { Log.Error(error, "Area settings unregistration failed"); }
            }
            var registered = locales;
            locales = null;
            if (registered == null || registered.Count == 0) return;
            var manager = Game.SceneFlow.GameManager.instance;
            var localization = manager == null ? null : manager.localizationManager;
            if (localization == null) return;
            registered.Unregister(localization.RemoveSource, (locale, error) => Log.Error(error, "Area localization removal failed: " + locale));
        }
        // Shared Area finance applies queued Area amounts after every GameSimulation producer (UpdateAt) and flushes them
        // before a save serializes city money. The two systems are one module: finance never runs without its save flush.
        // A registration failure leaves Area Core running; the finance producers (policy upkeep, Park) are then not registered.
        private static ModuleRegistration RegisterAreaFinance(UpdateSystem updateSystem)
        {
            var finance = new ModuleRegistration("Area finance",
                typeof(CS2AreaSystem.Core.Finance.AreaFinanceSystem), typeof(CS2AreaSystem.Core.Finance.AreaFinanceSaveSystem));
            try
            {
                updateSystem.UpdateAfter<CS2AreaSystem.Core.Finance.AreaFinanceSystem>(SystemUpdatePhase.GameSimulation); finance.Step();
                updateSystem.UpdateBefore<CS2AreaSystem.Core.Finance.AreaFinanceSaveSystem, Game.Serialization.SerializerSystem>(SystemUpdatePhase.Serialize); finance.Step();
                finance.Complete();
            }
            catch (System.Exception error) { finance.Fail(error, type => Disable(updateSystem, type), ReportRegistrationFailure); }
            // Policy upkeep is a GameSimulation producer (UpdateAt), so AreaFinanceSystem (UpdateAfter) applies it in the same frame.
            if (ModuleRegistration.Requires(finance, "Area policy upkeep", message => Log.Error(message)))
            {
                var upkeep = new ModuleRegistration("Area policy upkeep", typeof(CS2AreaSystem.Core.Policies.AreaPolicyUpkeepSystem));
                try { updateSystem.UpdateAt<CS2AreaSystem.Core.Policies.AreaPolicyUpkeepSystem>(SystemUpdatePhase.GameSimulation); upkeep.Step(); upkeep.Complete(); }
                catch (System.Exception error) { upkeep.Fail(error, type => Disable(updateSystem, type), ReportRegistrationFailure); }
            }
            return finance;
        }
        // Park gameplay observes the city after Core and books its money through Area finance, so it is registered only with
        // a complete Area finance module and only as a whole. A Park failure leaves Core geometry, lifecycle, persistence and
        // the other area types running.
        private static ModuleRegistration RegisterParkSystems(UpdateSystem updateSystem, ModuleRegistration finance)
        {
            if (!ModuleRegistration.Requires(finance, "Park gameplay", message => Log.Error(message))) return null;
            var park = new ModuleRegistration("Park gameplay",
                typeof(CS2AreaSystem.Modules.Park.ParkRuntimeSystem), typeof(CS2AreaSystem.Modules.Park.ParkHistorySystem), typeof(CS2AreaSystem.Modules.Park.ParkHistorySaveSystem),
                typeof(CS2AreaSystem.Modules.Park.ParkLandscapeSystem), typeof(CS2AreaSystem.Modules.Park.ParkVisitSystem),
                typeof(CS2AreaSystem.Modules.Park.ParkQualitySystem), typeof(CS2AreaSystem.Modules.Park.ParkFinanceSystem),
                typeof(CS2AreaSystem.Modules.Park.ParkFinanceSaveSystem), typeof(CS2AreaSystem.Modules.Park.ParkProgressionSystem));
            try
            {
                // The runtime owns the single preload generation for every Park consumer. Include it in the same
                // registration unit so a partial Park failure disables the owner as well as its consumers.
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Park.ParkRuntimeSystem>(SystemUpdatePhase.ModificationEnd); park.Step();
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Park.ParkHistorySystem, CS2AreaSystem.Modules.Park.ParkRuntimeSystem>(SystemUpdatePhase.ModificationEnd); park.Step();
                updateSystem.UpdateBefore<CS2AreaSystem.Modules.Park.ParkHistorySaveSystem, Game.Serialization.SerializerSystem>(SystemUpdatePhase.Serialize); park.Step();
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Park.ParkLandscapeSystem>(SystemUpdatePhase.ModificationEnd); park.Step();
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Park.ParkVisitSystem>(SystemUpdatePhase.GameSimulation); park.Step();
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Park.ParkQualitySystem>(SystemUpdatePhase.GameSimulation); park.Step();
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Park.ParkFinanceSystem>(SystemUpdatePhase.GameSimulation); park.Step();
                updateSystem.UpdateBefore<CS2AreaSystem.Modules.Park.ParkFinanceSaveSystem, CS2AreaSystem.Core.Finance.AreaFinanceSaveSystem>(SystemUpdatePhase.Serialize); park.Step();
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Park.ParkProgressionSystem>(SystemUpdatePhase.GameSimulation); park.Step();
                park.Complete();
            }
            catch (System.Exception error) { park.Fail(error, type => Disable(updateSystem, type), ReportRegistrationFailure); }
            return park;
        }
        // Park internal events (Phase 2 C5) charge city money through Area finance and change Park gameplay inputs, so they
        // need both modules complete. Completions arrive through Area finance's listener, also inside its save flush, so no
        // separate save system exists. A failure disables only the events (base Park gameplay keeps neutral event effects).
        private static void RegisterParkEvents(UpdateSystem updateSystem, ModuleRegistration finance, ModuleRegistration park)
        {
            if (!ModuleRegistration.Requires(finance, "Park events", message => Log.Error(message))) return;
            if (!ModuleRegistration.Requires(park, "Park events", message => Log.Error(message))) return;
            var events = new ModuleRegistration("Park events", typeof(CS2AreaSystem.Modules.Park.ParkEventSystem));
            try
            {
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Park.ParkEventSystem>(SystemUpdatePhase.GameSimulation); events.Step();
                events.Complete();
            }
            catch (System.Exception error) { events.Fail(error, type => Disable(updateSystem, type), ReportRegistrationFailure); }
        }
        // Phase 2 C6: the coverage writer runs directly after the native Park writer (same interval, so it inherits its
        // offset) and its values are restored before the serializer. The verified runtime gate is open; a live gameplay
        // option restores/suppresses the modifier without disabling Park gameplay. A registration failure disables only it.
        private static void RegisterParkCoverageModifier(UpdateSystem updateSystem, ModuleRegistration park)
        {
            if (!ModuleRegistration.Requires(park, "Park coverage modifier", message => Log.Error(message))) return;
            var coverage = new ModuleRegistration("Park coverage modifier",
                typeof(CS2AreaSystem.Modules.Park.ParkCoverageModifierSystem), typeof(CS2AreaSystem.Modules.Park.ParkCoverageModifierSaveSystem));
            try
            {
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Park.ParkCoverageModifierSystem, Game.Simulation.ParkAISystem>(SystemUpdatePhase.GameSimulation); coverage.Step();
                updateSystem.UpdateBefore<CS2AreaSystem.Modules.Park.ParkCoverageModifierSaveSystem, Game.Serialization.SerializerSystem>(SystemUpdatePhase.Serialize); coverage.Step();
                coverage.Complete();
            }
            catch (System.Exception error) { coverage.Fail(error, type => Disable(updateSystem, type), ReportRegistrationFailure); }
        }
        // Industrial gameplay observes the city after Core (runtime state in ModificationEnd after membership/activation,
        // hourly observation and progression in GameSimulation), as a whole. A registration failure leaves Area Core, Park
        // and Tourism running.
        private static ModuleRegistration RegisterIndustrialSystems(UpdateSystem updateSystem)
        {
            var industrial = new ModuleRegistration("Industrial gameplay",
                typeof(CS2AreaSystem.Modules.Industrial.IndustrialRuntimeSystem), typeof(CS2AreaSystem.Modules.Industrial.IndustrialHistorySystem), typeof(CS2AreaSystem.Modules.Industrial.IndustrialHistorySaveSystem), typeof(CS2AreaSystem.Modules.Industrial.IndustrialObservationSystem),
                typeof(CS2AreaSystem.Modules.Industrial.IndustrialPolicyEffectSystem), typeof(CS2AreaSystem.Modules.Industrial.IndustrialProgressionSystem));
            try
            {
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Industrial.IndustrialRuntimeSystem>(SystemUpdatePhase.ModificationEnd); industrial.Step();
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Industrial.IndustrialHistorySystem, CS2AreaSystem.Modules.Industrial.IndustrialRuntimeSystem>(SystemUpdatePhase.ModificationEnd); industrial.Step();
                updateSystem.UpdateBefore<CS2AreaSystem.Modules.Industrial.IndustrialHistorySaveSystem, Game.Serialization.SerializerSystem>(SystemUpdatePhase.Serialize); industrial.Step();
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Industrial.IndustrialObservationSystem>(SystemUpdatePhase.GameSimulation); industrial.Step();
                // Policy effects reach vanilla state before an observation cycle in the same frame reads it.
                updateSystem.UpdateBefore<CS2AreaSystem.Modules.Industrial.IndustrialPolicyEffectSystem, CS2AreaSystem.Modules.Industrial.IndustrialObservationSystem>(SystemUpdatePhase.GameSimulation); industrial.Step();
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Industrial.IndustrialProgressionSystem, CS2AreaSystem.Modules.Industrial.IndustrialObservationSystem>(SystemUpdatePhase.GameSimulation); industrial.Step();
                industrial.Complete();
            }
            catch (System.Exception error) { industrial.Fail(error, type => Disable(updateSystem, type), ReportRegistrationFailure); }
            return industrial;
        }
        // Industrial support payments (Phase 2 C4) charge city money through Area finance and credit companies resolved by
        // Industrial gameplay, so they need both modules complete. The scheduler (a GameSimulation producer, applied by
        // AreaFinanceSystem in the same frame) and its save flush (after Area finance's flush, before the serializer) are
        // one unit. A failure leaves Industrial observation, Core and Park running and the support controls unavailable.
        private static void RegisterIndustrialSupport(UpdateSystem updateSystem, ModuleRegistration finance, ModuleRegistration industrial)
        {
            if (!ModuleRegistration.Requires(finance, "Industrial support", message => Log.Error(message))) return;
            if (!ModuleRegistration.Requires(industrial, "Industrial support", message => Log.Error(message))) return;
            var support = new ModuleRegistration("Industrial support",
                typeof(CS2AreaSystem.Modules.Industrial.IndustrialSupportSystem), typeof(CS2AreaSystem.Modules.Industrial.IndustrialSupportSaveSystem));
            try
            {
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Industrial.IndustrialSupportSystem>(SystemUpdatePhase.GameSimulation); support.Step();
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Industrial.IndustrialSupportSaveSystem, CS2AreaSystem.Core.Finance.AreaFinanceSaveSystem>(SystemUpdatePhase.Serialize); support.Step();
                support.Complete();
            }
            catch (System.Exception error) { support.Fail(error, type => Disable(updateSystem, type), ReportRegistrationFailure); }
        }
        // Registered systems cannot be removed from UpdateSystem; a disabled system is skipped by ComponentSystemBase.Update.
        private static void Disable(UpdateSystem updateSystem, System.Type type)
        {
            var system = updateSystem.World.GetExistingSystemManaged(type);
            if (system != null) system.Enabled = false;
        }
        private static void ReportRegistrationFailure(System.Exception error, string message) => Log.Error(error, message);
        private void RegisterSettings()
        {
            try
            {
                var created = new CS2AreaSystem.Core.Settings.AreaSystemSettings(this);
                Colossal.IO.AssetDatabase.AssetDatabase.global.LoadSettings(nameof(CS2AreaSystem), created, new CS2AreaSystem.Core.Settings.AreaSystemSettings(this));
                created.RegisterInOptionsUI();
                settings = created;
                CS2AreaSystem.Core.Settings.AreaSettings.Use(created);
            }
            // Defaults stay in effect; the mod keeps working without its options page.
            catch (System.Exception error) { Log.Error(error, "Area settings unavailable; defaults are used"); }
        }
        private void RegisterLocalization()
        {
            locales = new LocalizationSourceRegistration<MemorySource>();
            try
            {
                var localization = Game.SceneFlow.GameManager.instance.localizationManager;
                locales.Register(localization.GetSupportedLocales(), locale => new MemorySource(NativeAreaLocalization.Entries(locale)),
                    localization.AddSource, (locale, error) => Log.Error(error, "Area localization unavailable for " + locale));
            }
            catch (System.Exception error) { Log.Error(error, "Area menu localization unavailable"); }
        }
    }
}
