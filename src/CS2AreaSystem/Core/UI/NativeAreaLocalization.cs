using System;
using System.Collections.Generic;
using CS2AreaSystem.Core.Prefabs;
namespace CS2AreaSystem.Core.UI
{
    public static class NativeAreaLocalization
    {
        public static Dictionary<string,string> Entries(string locale)
        {
            const string menu = AreaToolbarPrefabFactory.MenuName, category = AreaToolbarPrefabFactory.CategoryName;
            bool ja = locale != null && locale.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
            return new Dictionary<string,string> {
                ["Assets.NAME[" + menu + "]"] = ja ? "エリア" : "Areas",
                ["Assets.NAME[" + category + "]"] = ja ? "エリアの種類" : "Area Types",
                // Native toolbar tooltips (1.6.2f1 UI): menu button uses Services.*, category tab SubServices.NAME,
                // category details Assets.SUB_SERVICE_DESCRIPTION and asset details Assets.DESCRIPTION.
                ["Services.NAME[" + menu + "]"] = ja ? "エリア" : "Areas",
                ["Services.DESCRIPTION[" + menu + "]"] = ja ? "公園・産業・観光の管理エリアを作成します。管理境界は既存の建物や道路を消しません。" : "Create Park, Industrial and Tourism management areas. Boundaries never clear existing buildings or roads.",
                ["SubServices.NAME[" + category + "]"] = ja ? "エリアの種類" : "Area Types",
                ["Assets.SUB_SERVICE_DESCRIPTION[" + category + "]"] = ja ? "作成する管理エリアの種類を選びます。" : "Choose the kind of management area to draw.",
                ["Assets.DESCRIPTION[CS2AS.ParkArea]"] = ja ? "公園エリアの境界を描きます。エリア内の既存の建物を入口に割り当てると稼働します。" : "Draw a Park Area boundary. It becomes active once an existing building inside it is assigned as the entrance.",
                ["Assets.DESCRIPTION[CS2AS.IndustrialArea]"] = ja ? "産業エリアの境界を描きます。エリア内の既存の建物を本部に割り当てると稼働します。" : "Draw an Industrial Area boundary. It becomes active once an existing building inside it is assigned as the headquarters.",
                ["Assets.DESCRIPTION[CS2AS.TourismArea]"] = ja ? "観光エリアの境界を描きます。完成するとすぐに稼働します。" : "Draw a Tourism Area boundary. It becomes active as soon as it is completed.",
                ["Assets.NAME[CS2AS.ParkArea]"] = ja ? "公園エリア" : "Park Area",
                ["Assets.NAME[CS2AS.IndustrialArea]"] = ja ? "産業エリア" : "Industrial Area",
                ["Assets.NAME[CS2AS.TourismArea]"] = ja ? "観光エリア" : "Tourism Area",
                ["CS2AS.Type.Park"] = ja ? "公園エリア" : "Park Area",
                ["CS2AS.Type.Industrial"] = ja ? "産業エリア" : "Industrial Area",
                ["CS2AS.Type.Tourism"] = ja ? "観光エリア" : "Tourism Area",
                ["CS2AS.Type.Unavailable"] = ja ? "利用できないエリアモジュール" : "Unavailable area module",
                ["CS2AS.Tab.Overview"] = ja ? "概要" : "Overview",
                ["CS2AS.Summary.Active"] = ja ? "稼働中" : "Active",
                ["CS2AS.Summary.AnchorRequired"] = ja ? "このエリアを有効化するには必要な施設を割り当ててください。" : "Assign a required facility to activate this area.",
                ["CS2AS.Summary.ModuleUnavailable"] = ja ? "このエリアのモジュールは利用できません。保存済みの状態は保持されます。" : "This area's module is unavailable. Saved state is retained.",
                ["CS2AS.State.Created"] = ja ? "作成済み" : "Created",
                ["CS2AS.State.Inactive"] = ja ? "無効" : "Inactive",
                ["CS2AS.State.Active"] = ja ? "稼働中" : "Active",
                ["CS2AS.State.Suspended"] = ja ? "停止中" : "Suspended",
                ["CS2AS.State.ModuleUnavailable"] = ja ? "モジュール利用不可" : "Module unavailable",
                ["CS2AS.State.Deleted"] = ja ? "削除済み" : "Deleted",
                ["CS2AS.UI.AreaManagement"] = ja ? "エリア管理" : "Area management",
                ["CS2AS.UI.Level"] = ja ? "レベル" : "Level",
                ["CS2AS.UI.Experience"] = ja ? "経験値" : "Experience",
                ["CS2AS.UI.EditArea"] = ja ? "エリアを編集" : "Edit area",
                ["CS2AS.UI.Locate"] = ja ? "場所を表示" : "Locate",
                ["CS2AS.UI.AreaName"] = ja ? "エリア名" : "Area name",
                ["CS2AS.UI.Rename"] = ja ? "名前を変更" : "Rename",
                ["CS2AS.UI.AssignHeadquarters"] = ja ? "本部を割り当て" : "Assign headquarters",
                ["CS2AS.UI.AssignEntrance"] = ja ? "入口を割り当て" : "Assign entrance",
                ["CS2AS.UI.SelectAnchor"] = ja ? "このエリア内の既存の建物を選択してください。" : "Select an existing building inside this area.",
                ["CS2AS.UI.CancelAssignment"] = ja ? "割り当てをキャンセル" : "Cancel assignment",
                ["CS2AS.UI.DeleteArea"] = ja ? "エリアを削除" : "Delete area",
                ["CS2AS.UI.DeleteConfirm"] = ja ? "この管理境界を削除しますか？" : "Delete this management boundary?",
                ["CS2AS.UI.ConfirmDelete"] = ja ? "削除を確定" : "Confirm delete",
                ["CS2AS.UI.Cancel"] = ja ? "キャンセル" : "Cancel",
                ["CS2AS.UI.AreaList"] = ja ? "管理エリア一覧" : "Managed areas",
                ["CS2AS.UI.NoAreas"] = ja ? "管理エリアはまだありません。" : "No managed areas yet."
            };
        }
    }
}
