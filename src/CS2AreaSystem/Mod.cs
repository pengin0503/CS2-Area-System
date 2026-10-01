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
            updateSystem.UpdateAt<ManagedAreaLifecycleSystem>(SystemUpdatePhase.PreTool);
            updateSystem.UpdateAt<ManagedAreaLifecycleSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<CS2AreaSystem.Core.Policies.AreaPolicyBootstrapSystem, CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<AreaMembershipSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<CS2AreaSystem.Core.Anchors.AreaActivationSystem>(SystemUpdatePhase.ModificationEnd);
            try { updateSystem.UpdateAfter<CS2AreaSystem.Core.Policies.AreaPolicySystem, CS2AreaSystem.Core.Anchors.AreaActivationSystem>(SystemUpdatePhase.ModificationEnd); }
            catch (System.Exception error) { Log.Error(error, "Area policy runtime unavailable; Area Core and modules remain registered."); }
            RegisterParkSystems(updateSystem);
            RegisterIndustrialSystems(updateSystem);
            RegisterLocalization();
            RegisterSettings();
            try { updateSystem.UpdateAfter<AreaManagementUISystem>(SystemUpdatePhase.UIUpdate); }
            catch (System.Exception error) { Log.Error(error, "Area management UI registration failed; simulation remains registered."); }
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
        // Park gameplay observes the city after Core. A Park registration failure leaves Core geometry, lifecycle,
        // persistence and the other area types running.
        private static void RegisterParkSystems(UpdateSystem updateSystem)
        {
            try
            {
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Park.ParkLandscapeSystem>(SystemUpdatePhase.ModificationEnd);
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Park.ParkVisitSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Park.ParkQualitySystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Park.ParkFinanceSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<CS2AreaSystem.Modules.Park.ParkFinanceSaveSystem, Game.Serialization.SerializerSystem>(SystemUpdatePhase.Serialize);
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Park.ParkProgressionSystem>(SystemUpdatePhase.GameSimulation);
            }
            catch (System.Exception error) { Log.Error(error, "Park gameplay systems unavailable; Area Core remains registered."); }
        }
        // Industrial gameplay observes the city after Core (runtime state in ModificationEnd after membership/activation,
        // hourly observation and progression in GameSimulation). A registration failure leaves Area Core, Park and
        // Tourism running.
        private static void RegisterIndustrialSystems(UpdateSystem updateSystem)
        {
            try
            {
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Industrial.IndustrialRuntimeSystem>(SystemUpdatePhase.ModificationEnd);
                updateSystem.UpdateAt<CS2AreaSystem.Modules.Industrial.IndustrialObservationSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAfter<CS2AreaSystem.Modules.Industrial.IndustrialProgressionSystem, CS2AreaSystem.Modules.Industrial.IndustrialObservationSystem>(SystemUpdatePhase.GameSimulation);
            }
            catch (System.Exception error) { Log.Error(error, "Industrial gameplay systems unavailable; Area Core, Park and Tourism remain registered."); }
        }
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
