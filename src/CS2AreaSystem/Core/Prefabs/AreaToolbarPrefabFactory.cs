using Game.Prefabs;
using UnityEngine;
namespace CS2AreaSystem.Core.Prefabs
{
    public sealed class AreaToolbarPrefabFactory
    {
        public const string MenuName = "CS2AS Areas Menu";
        public const string CategoryName = "CS2AS Area Types";
        public static bool ShouldAttach(bool hostAvailable) => hostAvailable;
        public UIAssetMenuPrefab CreateAreasMenu(UIToolbarGroupPrefab hostGroup)
        {
            var menu = ScriptableObject.CreateInstance<UIAssetMenuPrefab>();
            menu.name = MenuName;
            var ui = menu.AddComponent<UIObject>();
            ui.m_Group = hostGroup;
            ui.m_Priority = 90;
            ui.m_Icon = ManagedAreaPrefabFactory.FallbackIconUri;
            return menu;
        }
        public UIAssetCategoryPrefab CreateAreaTypesCategory(UIAssetMenuPrefab menu)
        {
            var category = ScriptableObject.CreateInstance<UIAssetCategoryPrefab>();
            category.name = CategoryName;
            category.m_Menu = menu;
            var ui = category.AddComponent<UIObject>();
            ui.m_Priority = 0;
            ui.m_Icon = ManagedAreaPrefabFactory.FallbackIconUri;
            return category;
        }
        public void AttachAreaPrefab(PrefabBase areaPrefab, UIAssetCategoryPrefab category, int priority, string iconUri)
        {
            var ui = areaPrefab.GetComponent<UIObject>() ?? areaPrefab.AddComponent<UIObject>();
            ui.m_Group = category;
            ui.m_Priority = priority;
            ui.m_Icon = string.IsNullOrWhiteSpace(iconUri) ? ManagedAreaPrefabFactory.FallbackIconUri : iconUri;
        }
    }
}
