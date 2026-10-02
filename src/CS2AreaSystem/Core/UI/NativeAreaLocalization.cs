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
            var entries = new Dictionary<string,string> {
                ["Assets.NAME[" + menu + "]"] = ja ? "エリア" : "Areas",
                ["Assets.NAME[" + category + "]"] = ja ? "エリアの種類" : "Area Types",
                // Native toolbar tooltips (1.6.2f1 UI): menu button uses Services.*, category tab SubServices.NAME,
                // category details Assets.SUB_SERVICE_DESCRIPTION and asset details Assets.DESCRIPTION.
                ["Services.NAME[" + menu + "]"] = ja ? "エリア" : "Areas",
                ["Services.DESCRIPTION[" + menu + "]"] = ja ? "公園・産業・観光の管理エリアを作成します。管理境界は既存の建物や道路を消しません。" : "Create Park, Industrial and Tourism management areas. Boundaries never clear existing buildings or roads.",
                ["SubServices.NAME[" + category + "]"] = ja ? "エリアの種類" : "Area Types",
                ["Assets.SUB_SERVICE_DESCRIPTION[" + category + "]"] = ja ? "作成する管理エリアの種類を選びます。" : "Choose the kind of management area to draw.",
                ["Assets.DESCRIPTION[CS2AS.ParkArea]"] = ja ? "公園エリアの境界を描きます。エリア内の公園整備施設の本部と、境界を横断する専用歩道の入口がそろうと稼働します。" : "Draw a Park Area boundary. It requires a Park Maintenance headquarters and a dedicated pedestrian path crossing the boundary.",
                ["Assets.DESCRIPTION[CS2AS.IndustrialArea]"] = ja ? "産業エリアの境界を描きます。エリア内のオフィス建物を本部に割り当てると稼働します。" : "Draw an Industrial Area boundary. It becomes active once an office building inside it is assigned as the headquarters.",
                ["Assets.DESCRIPTION[CS2AS.TourismArea]"] = ja ? "観光エリアは現在、境界のみです。観光のゲームプレイ機能（観測・評価・進行）は未実装で、今後の更新で追加予定です。" : "Tourism is currently a boundary only. No tourism gameplay (observations, scoring or progression) is implemented; it is planned for a future update.",
                ["Assets.NAME[CS2AS.ParkArea]"] = ja ? "公園エリア" : "Park Area",
                ["Assets.NAME[CS2AS.IndustrialArea]"] = ja ? "産業エリア" : "Industrial Area",
                ["Assets.NAME[CS2AS.TourismArea]"] = ja ? "観光エリア" : "Tourism Area",
                ["CS2AS.Type.Park"] = ja ? "公園エリア" : "Park Area",
                ["CS2AS.Type.Industrial"] = ja ? "産業エリア" : "Industrial Area",
                ["CS2AS.Type.Tourism"] = ja ? "観光エリア" : "Tourism Area",
                ["CS2AS.Type.Unavailable"] = ja ? "利用できないエリアモジュール" : "Unavailable area module",
                ["CS2AS.Tab.Overview"] = ja ? "概要" : "Overview",
                ["CS2AS.Summary.Active"] = ja ? "稼働中" : "Active",
                ["CS2AS.Summary.TourismBoundaryOnly"] = ja ? "境界のみ。観光のゲームプレイ機能（観測・評価・進行）は未実装で、今後の更新で追加予定です。" : "Boundary only. No tourism gameplay (observations, scoring or progression) is implemented; it is planned for a future update.",
                ["CS2AS.Summary.AnchorRequired"] = ja ? "このエリアを有効化するには必要な施設を割り当ててください。" : "Assign a required facility to activate this area.",
                ["CS2AS.Summary.ModuleUnavailable"] = ja ? "このエリアのモジュールは利用できません。保存済みの状態は保持されます。" : "This area's module is unavailable. Saved state is retained.",
                ["CS2AS.State.Created"] = ja ? "作成済み" : "Created",
                ["CS2AS.State.Inactive"] = ja ? "無効" : "Inactive",
                ["CS2AS.State.Active"] = ja ? "稼働中" : "Active",
                ["CS2AS.State.Suspended"] = ja ? "停止中" : "Suspended",
                ["CS2AS.State.ModuleUnavailable"] = ja ? "モジュール利用不可" : "Module unavailable",
                ["CS2AS.State.Deleted"] = ja ? "削除済み" : "Deleted",
                ["CS2AS.State.Quarantined"] = ja ? "隔離中" : "Quarantined",
                ["CS2AS.Summary.Quarantined"] = ja ? "このエリアの保存データに矛盾があるか、ゲーム側の検索用形状を利用できないため、今回のセッションでは隔離しています。保存データと形状は変更されず、メンバー判定・有効化・条例・財務は動作しません。編集または削除のみ可能です。形状の編集または不要なエリアの削除後に再読み込みしてください。"
                    : "This area's saved data is inconsistent or its native search geometry is unavailable, so it is isolated for this session. Saved data and geometry are kept unchanged; membership, activation, policies and finance are disabled. Only Edit and Delete are available. Edit the geometry or delete the unwanted area, then reload.",
                ["CS2AS.UI.Level"] = ja ? "レベル" : "Level",
                ["CS2AS.UI.Experience"] = ja ? "経験値" : "Experience",
                ["CS2AS.UI.EditArea"] = ja ? "エリアを編集" : "Edit area",
                ["CS2AS.UI.AssignHeadquarters"] = ja ? "本部を割り当て" : "Assign headquarters",
                ["CS2AS.UI.AssignEntrance"] = ja ? "入口を割り当て" : "Assign entrance",
                ["CS2AS.UI.SelectAnchor"] = ja ? "このエリア内の既存の建物を選択してください。" : "Select an existing building inside this area.",
                ["CS2AS.UI.CancelAssignment"] = ja ? "割り当てをキャンセル" : "Cancel assignment",
                ["CS2AS.UI.DeleteArea"] = ja ? "エリアを削除" : "Delete area",
                ["CS2AS.UI.DeleteConfirm"] = ja ? "この管理境界を削除しますか？" : "Delete this management boundary?",
                ["CS2AS.UI.ConfirmDelete"] = ja ? "削除を確定" : "Confirm delete",
                ["CS2AS.UI.Cancel"] = ja ? "キャンセル" : "Cancel",
                ["CS2AS.UI.Type"] = ja ? "種類" : "Type",
                ["CS2AS.UI.Status"] = ja ? "状態" : "Status",
                ["CS2AS.UI.Entrance"] = ja ? "入口" : "Entrance",
                ["CS2AS.UI.Headquarters"] = ja ? "本部" : "Headquarters",
                ["CS2AS.UI.NotAssigned"] = ja ? "未割り当て" : "Not assigned",
                ["CS2AS.UI.Entrances"] = ja ? "入口" : "Entrances",
                ["CS2AS.UI.AddEntrance"] = ja ? "入口を追加" : "Add entrance",
                ["CS2AS.UI.RemoveEntrance"] = ja ? "解除" : "Remove",
                ["CS2AS.UI.AnchorInvalid"] = ja ? "条件を満たしていません" : "Not eligible",
                ["CS2AS.UI.AnchorRemoved"] = ja ? "撤去された建物" : "Building removed",
                ["CS2AS.UI.ChangeEntrance"] = ja ? "入口を変更" : "Change entrance",
                ["CS2AS.UI.ChangeHeadquarters"] = ja ? "本部を変更" : "Change headquarters",
                ["CS2AS.UI.SelectEntrance"] = ja ? "このエリア内の登録済み入口アセットをクリックしてください。" : "Click an explicitly registered entrance building inside this area.",
                ["CS2AS.UI.SelectHeadquarters"] = ja ? "このエリア内の本部候補をクリックしてください。公園は公園整備施設、工業はオフィス建物を使用します。" : "Click an eligible headquarters inside this area: Park Maintenance for a Park, or an office building for an Industrial Area.",
                ["CS2AS.UI.AreaList"] = ja ? "管理エリア一覧" : "Managed areas",
                ["CS2AS.UI.NoAreas"] = ja ? "管理エリアはまだありません。" : "No managed areas yet."
            };
            entries["CS2AS.UI.Met"] = ja ? "達成" : "Met";
            entries["CS2AS.UI.PathEntrances"] = ja ? "検出された歩道入口" : "Detected path entrances";
            entries["CS2AS.UI.PathEntrancesComplete"] = ja ? "専用歩道の境界横断を確認済み" : "Dedicated path boundary crossings checked";
            entries["CS2AS.UI.PathEntrancesUnknown"] = ja ? "歩道入口を調査中" : "Path entrances pending scan";
            entries["CS2AS.UI.NotMet"] = ja ? "未達成" : "Not met";
            // Runtime trend rows (Wave A1): direction label + signed change since the previous hourly sample.
            entries[CS2AreaSystem.Core.Diagnostics.RuntimeTrendPresentation.Up] = ja ? "前回比 上昇" : "vs previous: rising";
            entries[CS2AreaSystem.Core.Diagnostics.RuntimeTrendPresentation.Down] = ja ? "前回比 低下" : "vs previous: falling";
            entries[CS2AreaSystem.Core.Diagnostics.RuntimeTrendPresentation.Flat] = ja ? "前回比 横ばい" : "vs previous: unchanged";
            entries[CS2AreaSystem.Core.Diagnostics.RuntimeTrendPresentation.New] = ja ? "前回比 （記録開始）" : "vs previous: first sample";
            entries[CS2AreaSystem.Core.Diagnostics.RuntimeTrendPresentation.Tip] = ja
                ? "直前の項目について、1時間ごとの最新記録と前回記録の差です。記録はこのセッション中のみ保持され、ロード時にリセットされます。"
                : "Change of the item above between its latest and previous hourly samples. Samples are kept for this session only and reset on load.";
            CS2AreaSystem.Core.Settings.AreaSettingsLocalization.Append(entries, ja);
            CS2AreaSystem.Core.Policies.AreaPolicyLocalization.Append(entries, ja);
            // Budget page sub-items (vanilla EconomyPanel.BUDGET_SUB_ITEM keys, see UI/src/AreaBudgetDetails.tsx).
            entries["EconomyPanel.BUDGET_SUB_ITEM[" + CS2AreaSystem.Core.Finance.AreaBudgetViewModel.AreaManagementSource + "]"] = ja ? "エリア管理" : "Area Management";
            entries["EconomyPanel.BUDGET_SUB_ITEM[" + CS2AreaSystem.Core.Finance.AreaBudgetViewModel.AreaServiceFeesSource + "]"] = ja ? "エリアサービス料金" : "Area Service Fees";
            CS2AreaSystem.Modules.Park.ParkLocalization.Append(entries, ja);
            CS2AreaSystem.Modules.Industrial.IndustrialLocalization.Append(entries, ja);
            return entries;
        }
    }
}
