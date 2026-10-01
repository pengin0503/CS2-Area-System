using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.Settings
{
    // Option label/description keys, built with the same formulas as Game.Modding.ModSetting (1.6.2f1):
    // id = "<assembly>.<namespace>.<mod type>", name = settings type name.
    public static class AreaSettingsLocalization
    {
        public static string Id => typeof(Mod).Assembly.GetName().Name + "." + typeof(Mod).Namespace + "." + typeof(Mod).Name;
        public const string Name = nameof(AreaSystemSettings);
        public static string Section => "Options.SECTION[" + Id + "]";
        public static string Tab(string tab) => "Options.TAB[" + Id + "." + tab + "]";
        public static string Group(string group) => "Options.GROUP[" + Id + "." + group + "]";
        public static string Label(string option) => "Options.OPTION[" + Id + "." + Name + "." + option + "]";
        public static string Description(string option) => "Options.OPTION_DESCRIPTION[" + Id + "." + Name + "." + option + "]";
        public static string Warning(string option) => "Options.WARNING[" + Id + "." + Name + "." + option + "]";
        public static string EnumValue<T>(T value) where T : Enum => "Options." + Id + "." + typeof(T).Name.ToUpper() + "[" + value + "]";
        public static void Append(IDictionary<string, string> entries, bool ja)
        {
            entries[Section] = ja ? "CS2 エリアシステム" : "CS2 Area System";
            entries[Tab(AreaSystemSettings.MainTab)] = ja ? "全般" : "General";
            entries[Group(AreaSystemSettings.InterfaceGroup)] = ja ? "画面表示" : "Interface";
            entries[Group(AreaSystemSettings.GameplayGroup)] = ja ? "ゲームプレイ" : "Gameplay";
            entries[Group(AreaSystemSettings.ResetGroup)] = ja ? "リセット" : "Reset";
            Option(entries, nameof(AreaSystemSettings.ShowAreaList),
                ja ? "管理エリア一覧を初期展開" : "Expand managed area list by default",
                ja ? "オンにすると開始時に一覧を展開します。オフでは折りたたみますが、作成済みエリアを選択する一覧ボタンは常に表示します。ボタンでいつでも開閉できます。" : "When on, the list starts expanded. When off, it starts collapsed, but the button for selecting completed areas remains visible. Use the button to open or close it at any time.");
            Option(entries, nameof(AreaSystemSettings.FocusCameraOnSelect),
                ja ? "一覧から選んだエリアへカメラを移動" : "Move camera to areas picked from the list",
                ja ? "管理エリア一覧でエリアを選んだときに、カメラをそのエリアへ移動します。" : "When an area is picked in the managed area list, the camera moves to it.");
            Option(entries, nameof(AreaSystemSettings.PanelSide),
                ja ? "管理エリア一覧の表示位置" : "Managed area list position",
                ja ? "管理エリア一覧（と入口・本部の割り当て案内）を画面の右側・左側のどちらに表示するかを選びます。エリアの詳細はゲーム標準の選択パネルに表示されます。" : "Chooses the screen side for the managed area list and the anchor-assignment hint. Area details appear in the game's selected-info panel.");
            entries[EnumValue(AreaPanelSide.Right)] = ja ? "右" : "Right";
            entries[EnumValue(AreaPanelSide.Left)] = ja ? "左" : "Left";
            Option(entries, nameof(AreaSystemSettings.ConfirmAreaDeletion),
                ja ? "エリア削除時に確認する" : "Confirm area deletion",
                ja ? "管理パネルからエリアを削除する前に確認を表示します。" : "Asks for confirmation before deleting an area from the management panel.");
            Option(entries, nameof(AreaSystemSettings.AllowManagedAreaOverlap),
                ja ? "管理エリア同士の重なりを許可" : "Allow managed areas to overlap",
                ja ? "オンにすると管理エリア同士の重なりは警告になり、そのまま完成できます。オフにするとゲーム標準どおりエラーになります。ゲーム標準のエリアとの重なり、購入していないタイル、不正な形状は常にエラーです。" : "When on, overlaps between management areas are warnings and can be completed. When off they remain native errors. Overlaps with vanilla areas, unpurchased tiles and invalid shapes remain blocking errors.");
            Option(entries, nameof(AreaSystemSettings.ResetToDefaults),
                ja ? "設定を初期値に戻す" : "Reset settings to defaults",
                ja ? "このMODの設定をすべて初期値に戻します。" : "Restores every setting of this mod to its default value.");
            entries[Warning(nameof(AreaSystemSettings.ResetToDefaults))] = ja ? "設定を初期値に戻しますか？" : "Reset all settings to their defaults?";
        }
        private static void Option(IDictionary<string, string> entries, string option, string label, string description)
        { entries[Label(option)] = label; entries[Description(option)] = description; }
    }
}
