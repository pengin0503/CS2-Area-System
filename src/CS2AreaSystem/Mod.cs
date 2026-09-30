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
        internal static readonly ILog Log = LogManager.GetLogger(nameof(CS2AreaSystem));
        public void OnLoad(UpdateSystem updateSystem)
        {
            CS2AreaSystem.Core.Persistence.ComponentSchema.ReportUnsupported = message => Log.Error(message);
            updateSystem.UpdateBefore<AreaPrefabRegistrationSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateBefore<ManagedAreaOverlapSystem, Game.Notifications.IconCommandSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAt<ManagedAreaLifecycleSystem>(SystemUpdatePhase.PreTool);
            updateSystem.UpdateAt<ManagedAreaLifecycleSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<CS2AreaSystem.Core.Persistence.AreaLoadCoordinatorSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<AreaMembershipSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<CS2AreaSystem.Core.Anchors.AreaActivationSystem>(SystemUpdatePhase.ModificationEnd);
            RegisterLocalization();
            try { updateSystem.UpdateAfter<AreaManagementUISystem>(SystemUpdatePhase.UIUpdate); }
            catch (System.Exception error) { Log.Error(error, "Area management UI registration failed; simulation remains registered."); }
        }
        public void OnDispose()
        {
            CS2AreaSystem.Core.Persistence.ComponentSchema.ReportUnsupported = null;
            var registered = locales;
            locales = null;
            if (registered == null || registered.Count == 0) return;
            var manager = Game.SceneFlow.GameManager.instance;
            var localization = manager == null ? null : manager.localizationManager;
            if (localization == null) return;
            registered.Unregister(localization.RemoveSource, (locale, error) => Log.Error(error, "Area localization removal failed: " + locale));
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
