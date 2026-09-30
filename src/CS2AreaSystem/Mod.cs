using Game;
using CS2AreaSystem.Core.Membership;
using Game.Modding;
using Colossal.Logging;
using CS2AreaSystem.Core.Prefabs;
using Game.Prefabs;
using CS2AreaSystem.Core.Areas;
namespace CS2AreaSystem
{
    public sealed class Mod : IMod
    {
        private readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Colossal.Localization.MemorySource>> locales = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Colossal.Localization.MemorySource>>();
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
            try
            {
                var localization = Game.SceneFlow.GameManager.instance.localizationManager;
                foreach (var locale in localization.GetSupportedLocales())
                {
                    var source = new Colossal.Localization.MemorySource(Core.UI.NativeAreaLocalization.Entries(locale));
                    localization.AddSource(locale, source); locales.Add(new System.Collections.Generic.KeyValuePair<string, Colossal.Localization.MemorySource>(locale, source));
                }
            }
            catch (System.Exception error) { Log.Error(error, "Area menu localization unavailable"); }
            try { updateSystem.UpdateAfter<CS2AreaSystem.Core.UI.AreaManagementUISystem>(SystemUpdatePhase.UIUpdate); }
            catch (System.Exception error) { Log.Error(error, "Area management UI registration failed; simulation remains registered."); }
        }
        public void OnDispose()
        {
            CS2AreaSystem.Core.Persistence.ComponentSchema.ReportUnsupported = null;
            foreach (var locale in locales) Game.SceneFlow.GameManager.instance.localizationManager.RemoveSource(locale.Key, locale.Value);
            locales.Clear();
        }
    }
}
