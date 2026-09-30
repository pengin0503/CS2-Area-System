using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
namespace CS2AreaSystem.Core.Settings
{
    public enum AreaPanelSide { Right, Left }
    public interface IAreaSettings
    {
        bool ShowAreaList { get; }
        bool FocusCameraOnSelect { get; }
        AreaPanelSide PanelSide { get; }
        bool ConfirmAreaDeletion { get; }
        bool AllowManagedAreaOverlap { get; }
    }
    public sealed class AreaSettingsDefaults : IAreaSettings
    {
        public static readonly AreaSettingsDefaults Instance = new AreaSettingsDefaults();
        public bool ShowAreaList => true;
        public bool FocusCameraOnSelect => true;
        public AreaPanelSide PanelSide => AreaPanelSide.Right;
        public bool ConfirmAreaDeletion => true;
        public bool AllowManagedAreaOverlap => true;
    }
    // Live settings used by systems; falls back to defaults before OnLoad, after OnDispose and in tests.
    public static class AreaSettings
    {
        private static IAreaSettings current;
        public static IAreaSettings Current => current ?? AreaSettingsDefaults.Instance;
        internal static void Use(IAreaSettings settings) => current = settings;
    }
    // Options → CS2 Area System. Stored in ModsSettings/CS2AreaSystem/CS2AreaSystem.coc like other mods' settings.
    [FileLocation("ModsSettings/" + nameof(CS2AreaSystem) + "/" + nameof(CS2AreaSystem))]
    [SettingsUITabOrder(MainTab)]
    [SettingsUIGroupOrder(InterfaceGroup, GameplayGroup, ResetGroup)]
    [SettingsUIShowGroupName(InterfaceGroup, GameplayGroup)]
    public sealed class AreaSystemSettings : ModSetting, IAreaSettings
    {
        public const string MainTab = "Main", InterfaceGroup = "Interface", GameplayGroup = "Gameplay", ResetGroup = "Reset";
        public AreaSystemSettings(IMod mod) : base(mod) { SetDefaults(); }
        [SettingsUISection(MainTab, InterfaceGroup)] public bool ShowAreaList { get; set; }
        [SettingsUISection(MainTab, InterfaceGroup)] public bool FocusCameraOnSelect { get; set; }
        [SettingsUISection(MainTab, InterfaceGroup)] public AreaPanelSide PanelSide { get; set; }
        [SettingsUISection(MainTab, InterfaceGroup)] public bool ConfirmAreaDeletion { get; set; }
        [SettingsUISection(MainTab, GameplayGroup)] public bool AllowManagedAreaOverlap { get; set; }
        [SettingsUISection(MainTab, ResetGroup)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool ResetToDefaults { set { SetDefaults(); ApplyAndSave(); } }
        public override void SetDefaults()
        {
            var defaults = AreaSettingsDefaults.Instance;
            ShowAreaList = defaults.ShowAreaList;
            FocusCameraOnSelect = defaults.FocusCameraOnSelect;
            PanelSide = defaults.PanelSide;
            ConfirmAreaDeletion = defaults.ConfirmAreaDeletion;
            AllowManagedAreaOverlap = defaults.AllowManagedAreaOverlap;
        }
    }
}
