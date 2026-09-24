using UnityEditor;

namespace AvatarMeshMaterialOptimizer
{
    internal enum OptimizerLanguage
    {
        Japanese,
        English,
        Chinese,
        Korean
    }

    internal static class OptimizerLocalization
    {
        private const string PrefKey = "AzipaTools.MeshMaterialCombiner.Language";
        public static OptimizerLanguage Language
        {
            get => (OptimizerLanguage)EditorPrefs.GetInt(PrefKey, (int)OptimizerLanguage.Japanese);
            set => EditorPrefs.SetInt(PrefKey, (int)value);
        }

        public static string[] LanguageLabels => new[] { "日本語", "English", "简体中文", "한국어" };

        public static string T(string english)
        {
            var managedUi = ManagedBuildUi(english, Language);
            if (managedUi != null) return managedUi;
            switch (Language)
            {
                case OptimizerLanguage.Japanese: return Japanese(english);
                case OptimizerLanguage.Chinese: return Chinese(english);
                case OptimizerLanguage.Korean: return Korean(english);
                default: return English(english);
            }
        }

        private static string ManagedBuildUi(string key, OptimizerLanguage language)
        {
            if (key == "Status Valid")
                return language == OptimizerLanguage.Japanese ? "統合済み" : language == OptimizerLanguage.Chinese ? "已合并" : language == OptimizerLanguage.Korean ? "병합 완료" : "Merged";
            if (key == "History Filter Valid")
                return language == OptimizerLanguage.Japanese ? "問題なし" : language == OptimizerLanguage.Chinese ? "无问题" : language == OptimizerLanguage.Korean ? "문제 없음" : "No Issues";
            switch (key)
            {
                case "Preset Safe Label": return language == OptimizerLanguage.Japanese ? "Safe（推奨）" : language == OptimizerLanguage.Chinese ? "Safe（推荐）" : language == OptimizerLanguage.Korean ? "Safe(권장)" : "Safe (Recommended)";
                case "Preset Compatible Merge Label": return language == OptimizerLanguage.Japanese ? "Compatible Merge" : language == OptimizerLanguage.Chinese ? "Compatible Merge（兼容合并）" : language == OptimizerLanguage.Korean ? "Compatible Merge(호환 병합)" : "Compatible Merge";
                case "Preset Force Single Slot Label": return language == OptimizerLanguage.Japanese ? "Force Single Slot（Beta）" : language == OptimizerLanguage.Chinese ? "Force Single Slot（Beta）" : language == OptimizerLanguage.Korean ? "Force Single Slot(Beta)" : "Force Single Slot (Beta)";
                case "Preset Safe Description": return language == OptimizerLanguage.Japanese ? "Safe（推奨）: 異なるMaterialは統合しません。同じMaterialを参照する重複Slotだけを削減し、UVと元のMaterial設定を保持します。" : language == OptimizerLanguage.Chinese ? "Safe（推荐）：不合并不同材质。只减少引用同一材质的重复槽位，并保留UV和原材质设置。" : language == OptimizerLanguage.Korean ? "Safe(권장): 서로 다른 머티리얼은 병합하지 않습니다. 같은 머티리얼을 참조하는 중복 슬롯만 줄이고 UV와 원본 설정을 유지합니다." : "Safe (Recommended): Different Materials are not merged. Only duplicate slots that reference the same Material are reduced, preserving UVs and source Material settings.";
                case "Preset Compatible Merge Description": return language == OptimizerLanguage.Japanese ? "Compatible Merge: ShaderとAtlas化されない設定が互換のMaterialだけを統合します。対応するBase、Normal、Emission、Shadow、Mask Textureは同じ配置でAtlas化します。" : language == OptimizerLanguage.Chinese ? "Compatible Merge：仅合并Shader和非图集设置兼容的材质。支持的Base、Normal、Emission、Shadow和Mask纹理会使用相同布局生成图集。" : language == OptimizerLanguage.Korean ? "Compatible Merge: Shader와 아틀라스화되지 않는 설정이 호환되는 머티리얼만 병합합니다. 지원되는 Base, Normal, Emission, Shadow, Mask 텍스처를 같은 배치로 아틀라스화합니다." : "Compatible Merge: Materials are merged only when their Shader and non-atlased settings are compatible. Supported Base, Normal, Emission, Shadow, and mask textures are atlased together.";
                case "Managed Builds": return language == OptimizerLanguage.Japanese ? "統合履歴・管理" : language == OptimizerLanguage.Chinese ? "合并记录管理" : language == OptimizerLanguage.Korean ? "병합 기록 관리" : "Managed Builds";
                case "Avatar Reduction Preview": return language == OptimizerLanguage.Japanese ? "アバター全体の削減プレビュー" : language == OptimizerLanguage.Chinese ? "Avatar整体削减预览" : language == OptimizerLanguage.Korean ? "아바타 전체 절감 미리보기" : "Avatar Reduction Preview";
                case "History Records Included": return language == OptimizerLanguage.Japanese ? "対象履歴 {0}件" : language == OptimizerLanguage.Chinese ? "包含记录 {0}项" : language == OptimizerLanguage.Korean ? "대상 기록 {0}개" : "{0} history records";
                case "Avatar Reduction Preview Unavailable": return language == OptimizerLanguage.Japanese ? "対象Sceneまたは履歴の情報を解決できないため、削減プレビューを表示できません。" : language == OptimizerLanguage.Chinese ? "无法解析目标Scene或历史信息，因此无法显示削减预览。" : language == OptimizerLanguage.Korean ? "대상 Scene 또는 기록을 확인할 수 없어 절감 미리보기를 표시할 수 없습니다." : "The reduction preview is unavailable because the target Scene or history could not be resolved.";
                case "Refresh": return language == OptimizerLanguage.Japanese ? "更新" : language == OptimizerLanguage.Chinese ? "刷新" : language == OptimizerLanguage.Korean ? "새로 고침" : "Refresh";
                case "No Managed Builds": return language == OptimizerLanguage.Japanese ? "管理中の統合済みオブジェクトはありません。" : language == OptimizerLanguage.Chinese ? "当前没有受管理的合并对象。" : language == OptimizerLanguage.Korean ? "관리 중인 병합 오브젝트가 없습니다." : "No merged outputs are currently managed.";
                case "Created At": return language == OptimizerLanguage.Japanese ? "作成日時" : language == OptimizerLanguage.Chinese ? "创建时间" : language == OptimizerLanguage.Korean ? "생성 시간" : "Created At";
                case "Merged Output": return language == OptimizerLanguage.Japanese ? "統合先オブジェクト" : language == OptimizerLanguage.Chinese ? "合并输出对象" : language == OptimizerLanguage.Korean ? "병합 출력 오브젝트" : "Merged Output";
                case "Source Objects": return language == OptimizerLanguage.Japanese ? "ソースオブジェクト数" : language == OptimizerLanguage.Chinese ? "源对象数量" : language == OptimizerLanguage.Korean ? "소스 오브젝트 수" : "Source Objects";
                case "Source Count": return language == OptimizerLanguage.Japanese ? "ソース" : language == OptimizerLanguage.Chinese ? "源对象" : language == OptimizerLanguage.Korean ? "소스" : "Sources";
                case "Source List": return language == OptimizerLanguage.Japanese ? "ソースオブジェクト一覧" : language == OptimizerLanguage.Chinese ? "源对象列表" : language == OptimizerLanguage.Korean ? "소스 오브젝트 목록" : "Source Object List";
                case "Find Related Builds": return language == OptimizerLanguage.Japanese ? "関連する統合履歴を検索" : language == OptimizerLanguage.Chinese ? "查找相关合并记录" : language == OptimizerLanguage.Korean ? "관련 병합 기록 검색" : "Find Related Builds";
                case "Related Root": return language == OptimizerLanguage.Japanese ? "対象ルート" : language == OptimizerLanguage.Chinese ? "目标根对象" : language == OptimizerLanguage.Korean ? "대상 루트" : "Related Root";
                case "Use Selected Object": return language == OptimizerLanguage.Japanese ? "選択中のオブジェクトを設定" : language == OptimizerLanguage.Chinese ? "使用当前选中对象" : language == OptimizerLanguage.Korean ? "선택한 오브젝트 사용" : "Use Selected Object";
                case "Clear Filter": return language == OptimizerLanguage.Japanese ? "絞り込みを解除" : language == OptimizerLanguage.Chinese ? "清除筛选" : language == OptimizerLanguage.Korean ? "필터 해제" : "Clear Filter";
                case "Related Build Count": return language == OptimizerLanguage.Japanese ? "関連する統合履歴" : language == OptimizerLanguage.Chinese ? "相关合并记录" : language == OptimizerLanguage.Korean ? "관련 병합 기록" : "Related Builds";
                case "Last Built At": return language == OptimizerLanguage.Japanese ? "最終ビルド" : language == OptimizerLanguage.Chinese ? "最后构建" : language == OptimizerLanguage.Korean ? "마지막 빌드" : "Last Built";
                case "Revisions": return language == OptimizerLanguage.Japanese ? "Revision数" : language == OptimizerLanguage.Chinese ? "修订数量" : language == OptimizerLanguage.Korean ? "리비전 수" : "Revisions";
                case "Rebuild": return language == OptimizerLanguage.Japanese ? "再統合" : language == OptimizerLanguage.Chinese ? "重新合并" : language == OptimizerLanguage.Korean ? "다시 병합" : "Re-merge";
                case "Avatar Operations": return language == OptimizerLanguage.Japanese ? "アバター操作" : language == OptimizerLanguage.Chinese ? "Avatar操作" : language == OptimizerLanguage.Korean ? "아바타 작업" : "Avatar Operations";
                case "Suspend For Editing": return language == OptimizerLanguage.Japanese ? "編集のため一時解除" : language == OptimizerLanguage.Chinese ? "暂时解除以编辑" : language == OptimizerLanguage.Korean ? "편집을 위해 일시 해제" : "Suspend for Editing";
                case "Suspend For Editing Confirmation": return language == OptimizerLanguage.Japanese ? "ソースを元の状態へ戻し、統合先を非アクティブ化します。履歴・Recipe・Revision・生成アセットは保持されます。" : language == OptimizerLanguage.Chinese ? "恢复源对象并停用合并输出。历史、配方、修订和生成资源将保留。" : language == OptimizerLanguage.Korean ? "소스를 원래 상태로 복원하고 병합 출력을 비활성화합니다. 기록, Recipe, Revision 및 생성 에셋은 유지됩니다." : "Restore the sources and disable the merged output. History, recipe, revisions, and generated assets will be kept.";
                case "Suspend Avatar For Editing": return language == OptimizerLanguage.Japanese ? "一時一括解除（改変時）" : language == OptimizerLanguage.Chinese ? "批量暂时解除（编辑时）" : language == OptimizerLanguage.Korean ? "일괄 일시 해제(편집 시)" : "Suspend All for Editing";
                case "Suspend Avatar Confirmation": return language == OptimizerLanguage.Japanese ? "このアバターの統合履歴を依存関係の逆順ですべて一時解除します。1件でも失敗した場合は全件を元に戻します。" : language == OptimizerLanguage.Chinese ? "将按依赖关系的逆序暂时解除此Avatar的所有合并。任一操作失败时将全部回滚。" : language == OptimizerLanguage.Korean ? "이 아바타의 모든 병합을 의존성 역순으로 일시 해제합니다. 하나라도 실패하면 전체를 되돌립니다." : "Suspend every managed build for this avatar in reverse dependency order. All changes are rolled back if any operation fails.";
                case "Prepare Avatar For Upload": return language == OptimizerLanguage.Japanese ? "一括統合（アップロード前）" : language == OptimizerLanguage.Chinese ? "全部合并（上传前）" : language == OptimizerLanguage.Korean ? "일괄 병합(업로드 전)" : "Merge All Before Upload";
                case "Prepare Avatar Confirmation": return language == OptimizerLanguage.Japanese ? "このアバターの全履歴を依存順に再ビルドします。編集状態を新しい復元基準として保存し、成功後にソースを再整理します。自動ビルドは行いません。" : language == OptimizerLanguage.Chinese ? "按依赖顺序重新构建此Avatar的全部历史。当前编辑状态将成为新的恢复基准。不会启用自动构建。" : language == OptimizerLanguage.Korean ? "이 아바타의 모든 기록을 의존성 순서로 다시 빌드합니다. 현재 편집 상태를 새 복원 기준으로 저장하며 자동 빌드는 사용하지 않습니다." : "Rebuild every managed build for this avatar in dependency order. The edited state becomes the new restore baseline. Automatic build is not enabled.";
                case "Fully Detach": return language == OptimizerLanguage.Japanese ? "完全解除" : language == OptimizerLanguage.Chinese ? "完全解除合并" : language == OptimizerLanguage.Korean ? "병합 완전 해제" : "Fully Detach";
                case "Fully Detach Confirmation": return language == OptimizerLanguage.Japanese ? "ソースを復元し、統合先と全Revisionの生成アセットを削除して履歴を解除します。\n\n削除対象の生成アセット: {0}件\n他の履歴または開いているSceneから参照されているアセットは保持されます。削除失敗時はCleanupPendingとして履歴を残します。" : language == OptimizerLanguage.Chinese ? "恢复源对象，删除合并输出及全部修订的生成资源，并移除历史。\n\n目标生成资源：{0}\n受其他历史或已打开Scene引用的资源会保留。删除失败时历史将保留为CleanupPending。" : language == OptimizerLanguage.Korean ? "소스를 복원하고 병합 출력과 모든 Revision의 생성 에셋을 삭제한 뒤 기록을 해제합니다.\n\n삭제 대상 생성 에셋: {0}개\n다른 기록이나 열린 Scene에서 참조하는 에셋은 유지됩니다. 삭제 실패 시 CleanupPending으로 기록을 유지합니다." : "Restore the sources, delete the merged output and generated assets for every revision, then remove the history.\n\nGenerated assets targeted: {0}\nAssets referenced by another history or an open Scene are retained. The record remains CleanupPending if deletion fails.";
                case "Lifecycle Suspended": return language == OptimizerLanguage.Japanese ? "編集中" : language == OptimizerLanguage.Chinese ? "编辑中" : language == OptimizerLanguage.Korean ? "편집 중" : "Editing";
                case "Lifecycle Cleanup Pending": return language == OptimizerLanguage.Japanese ? "削除未完了" : language == OptimizerLanguage.Chinese ? "清理未完成" : language == OptimizerLanguage.Korean ? "정리 미완료" : "Cleanup Pending";
                case "Build Suspended": return language == OptimizerLanguage.Japanese ? "{0}個のソースを復元し、統合を一時解除しました。" : language == OptimizerLanguage.Chinese ? "已恢复{0}个源对象并暂时解除合并。" : language == OptimizerLanguage.Korean ? "소스 {0}개를 복원하고 병합을 일시 해제했습니다." : "Restored {0} source object(s) and suspended the build.";
                case "Avatar Suspended": return language == OptimizerLanguage.Japanese ? "{0}件の統合を一時解除しました。" : language == OptimizerLanguage.Chinese ? "已暂时解除{0}项合并。" : language == OptimizerLanguage.Korean ? "병합 {0}건을 일시 해제했습니다." : "Suspended {0} managed build(s).";
                case "Avatar Prepared": return language == OptimizerLanguage.Japanese ? "{0}件の統合が完了しました。" : language == OptimizerLanguage.Chinese ? "已重新构建{0}项，可进行上传。" : language == OptimizerLanguage.Korean ? "{0}건을 다시 빌드하여 업로드 가능한 상태로 준비했습니다." : "Rebuilt {0} managed build(s); the avatar is ready for upload.";
                case "Already Suspended": return language == OptimizerLanguage.Japanese ? "この統合はすでに一時解除されています。" : language == OptimizerLanguage.Chinese ? "此合并已暂时解除。" : language == OptimizerLanguage.Korean ? "이 병합은 이미 일시 해제되었습니다." : "This build is already suspended.";
                case "Suspend Output Missing": return language == OptimizerLanguage.Japanese ? "統合先GameObjectを保持できないため一時解除できません。" : language == OptimizerLanguage.Chinese ? "找不到要保留的合并输出GameObject，无法暂时解除。" : language == OptimizerLanguage.Korean ? "유지할 병합 출력 GameObject를 찾을 수 없어 일시 해제할 수 없습니다." : "The merged output GameObject is missing and cannot be preserved for suspension.";
                case "Cleanup Pending Cannot Suspend": return language == OptimizerLanguage.Japanese ? "生成物の削除が完了していないため一時解除できません。完全解除を再実行してください。" : language == OptimizerLanguage.Chinese ? "生成资源清理尚未完成。请重试完全解除。" : language == OptimizerLanguage.Korean ? "생성물 정리가 완료되지 않았습니다. 완전 해제를 다시 실행하세요." : "Generated-asset cleanup is incomplete. Retry Fully Detach.";
                case "Cleanup Pending Cannot Rebuild": return language == OptimizerLanguage.Japanese ? "生成物の削除が完了していない履歴は再ビルドできません。完全解除を再実行してください。" : language == OptimizerLanguage.Chinese ? "清理未完成的历史无法重新构建。请重试完全解除。" : language == OptimizerLanguage.Korean ? "정리가 완료되지 않은 기록은 다시 빌드할 수 없습니다. 완전 해제를 다시 실행하세요." : "A CleanupPending history cannot be rebuilt. Retry Fully Detach.";
                case "Avatar Suspend Preflight Failed": return language == OptimizerLanguage.Japanese ? "一時解除の事前検証に失敗しました。" : language == OptimizerLanguage.Chinese ? "暂时解除预检失败。" : language == OptimizerLanguage.Korean ? "일시 해제 사전 검사에 실패했습니다." : "Suspend preflight failed.";
                case "External Build Dependency": return language == OptimizerLanguage.Japanese ? "このアバターの外部にある統合履歴へ依存しているため、一括処理できません。関連履歴を確認してください。" : language == OptimizerLanguage.Chinese ? "存在此Avatar之外的合并历史依赖，无法批量处理。" : language == OptimizerLanguage.Korean ? "이 아바타 외부의 병합 기록에 의존하므로 일괄 처리할 수 없습니다." : "The avatar depends on a managed build outside this avatar group, so the batch operation was cancelled.";
                case "Detach Has Dependent Build": return language == OptimizerLanguage.Japanese ? "この統合結果を使用している履歴「{0}」があります。依存する履歴を先に完全解除してください。" : language == OptimizerLanguage.Chinese ? "合并历史“{0}”正在使用此输出。请先完全解除依赖历史。" : language == OptimizerLanguage.Korean ? "이 출력을 사용하는 병합 기록 '{0}'이 있습니다. 의존하는 기록을 먼저 완전 해제하세요." : "Managed build '{0}' uses this output. Fully detach dependent builds first.";
                case "Suspend Has Active Dependent Build": return language == OptimizerLanguage.Japanese ? "この統合結果を使用中の履歴「{0}」があります。アバター単位の一時解除を使用するか、依存側を先に一時解除してください。" : language == OptimizerLanguage.Chinese ? "合并历史“{0}”正在使用此输出。请使用Avatar批量暂时解除，或先解除依赖项。" : language == OptimizerLanguage.Korean ? "이 출력을 사용하는 병합 기록 '{0}'이 있습니다. 아바타 단위 일시 해제를 사용하거나 의존하는 기록을 먼저 해제하세요." : "Managed build '{0}' currently uses this output. Suspend the avatar as a group, or suspend the dependent build first.";
                case "Rebuild Dependency Suspended": return language == OptimizerLanguage.Japanese ? "依存する統合「{0}」が一時解除中です。アバター単位の「アップロード準備」を使用してください。" : language == OptimizerLanguage.Chinese ? "依赖的合并“{0}”处于暂时解除状态。请使用Avatar的“准备上传”。" : language == OptimizerLanguage.Korean ? "의존하는 병합 '{0}'이 일시 해제 상태입니다. 아바타 단위 업로드 준비를 사용하세요." : "Dependency '{0}' is suspended. Use the avatar-level Prepare for Upload action.";
                case "Rebuild Confirmation": return language == OptimizerLanguage.Japanese ? "現在のソースから新しいMesh・Material・Textureを生成し、既存の統合先Rendererへ安全に差し替えますか？古いRevisionのアセットは保持されます。" : language == OptimizerLanguage.Chinese ? "要从当前源对象生成新的Mesh、Material和Texture，并安全替换现有合并输出Renderer吗？旧修订资源将被保留。" : language == OptimizerLanguage.Korean ? "현재 소스에서 새 Mesh, Material, Texture를 생성하고 기존 병합 출력 Renderer에 안전하게 적용할까요? 이전 리비전 에셋은 유지됩니다." : "Generate new Mesh, Material and Texture assets from the current sources and safely replace the existing merged Renderer? Previous revision assets will be kept.";
                case "Rebuild Info Missing": return language == OptimizerLanguage.Japanese ? "再ビルド情報なし" : language == OptimizerLanguage.Chinese ? "无重新构建信息" : language == OptimizerLanguage.Korean ? "재빌드 정보 없음" : "No Rebuild Info";
                case "Cannot Rebuild": return language == OptimizerLanguage.Japanese ? "再ビルド不可" : language == OptimizerLanguage.Chinese ? "无法重新构建" : language == OptimizerLanguage.Korean ? "다시 빌드 불가" : "Cannot Rebuild";
                case "Source Conflict": return language == OptimizerLanguage.Japanese ? "ソース競合" : language == OptimizerLanguage.Chinese ? "源对象冲突" : language == OptimizerLanguage.Korean ? "소스 충돌" : "Source Conflict";
                case "Source Ownership Conflict": return language == OptimizerLanguage.Japanese ? "「{0}」は統合履歴「{1}」（{2}）ですでに管理されています。その履歴を再統合するか、完全解除してから新しい統合を作成してください。" : language == OptimizerLanguage.Chinese ? "“{0}”已由合并历史“{1}”（{2}）管理。请重新合并该历史，或完全解除后再创建新的合并。" : language == OptimizerLanguage.Korean ? "'{0}'은(는) 병합 기록 '{1}'({2})에서 이미 관리 중입니다. 해당 기록을 다시 병합하거나 완전 해제한 뒤 새 병합을 만드세요." : "'{0}' is already managed by build history '{1}' ({2}). Re-merge that history, or Fully Detach it before creating a new merge.";
                case "Rebuild Recipe Missing": return language == OptimizerLanguage.Japanese ? "この履歴は旧バージョンで作成されているため、再ビルド設定がありません。" : language == OptimizerLanguage.Chinese ? "此记录由旧版本创建，没有重新构建设置。" : language == OptimizerLanguage.Korean ? "이 기록은 이전 버전에서 생성되어 재빌드 설정이 없습니다." : "This record was created by an older version and has no rebuild recipe.";
                case "Rebuild Scene Not Loaded": return language == OptimizerLanguage.Japanese ? "対象Sceneを開いてから再ビルドしてください。" : language == OptimizerLanguage.Chinese ? "请打开目标场景后重新构建。" : language == OptimizerLanguage.Korean ? "대상 씬을 연 후 다시 빌드하세요." : "Open the target Scene before rebuilding.";
                case "Rebuild Up To Date": return language == OptimizerLanguage.Japanese ? "統合元は最終ビルド時点から変更されていません。" : language == OptimizerLanguage.Chinese ? "源内容自上次构建后未发生变化。" : language == OptimizerLanguage.Korean ? "소스는 마지막 빌드 이후 변경되지 않았습니다." : "Sources have not changed since the last build.";
                case "Rebuild Validation Failed": return language == OptimizerLanguage.Japanese ? "現在のソースに検証エラーがあるため再ビルドできません。新規統合画面で内容を確認してください。" : language == OptimizerLanguage.Chinese ? "当前源对象存在验证错误，无法重新构建。请在新建合并页面中检查。" : language == OptimizerLanguage.Korean ? "현재 소스에 검증 오류가 있어 다시 빌드할 수 없습니다. 새 병합 화면에서 확인하세요." : "The current sources have validation errors. Review them in the New Merge screen.";
                case "Rebuild Completed": return language == OptimizerLanguage.Japanese ? "再ビルドが完了しました。" : language == OptimizerLanguage.Chinese ? "重新构建完成。" : language == OptimizerLanguage.Korean ? "다시 빌드가 완료되었습니다." : "Rebuild completed.";
                case "Rebuild Source Missing": return language == OptimizerLanguage.Japanese ? "再ビルド対象のRendererが見つかりません" : language == OptimizerLanguage.Chinese ? "找不到重新构建所需的Renderer" : language == OptimizerLanguage.Korean ? "재빌드 대상 Renderer를 찾을 수 없습니다" : "A source Renderer required for rebuilding could not be found";
                case "Rebuild Mesh Missing": return language == OptimizerLanguage.Japanese ? "再ビルド対象のMeshが見つかりません" : language == OptimizerLanguage.Chinese ? "找不到重新构建所需的Mesh" : language == OptimizerLanguage.Korean ? "재빌드 대상 Mesh를 찾을 수 없습니다" : "A source Mesh required for rebuilding could not be found";
                case "Rebuild Dependency Cycle": return language == OptimizerLanguage.Japanese ? "統合履歴同士が循環参照しているため、一括再ビルドできません。" : language == OptimizerLanguage.Chinese ? "合并记录之间存在循环依赖，无法批量重新构建。" : language == OptimizerLanguage.Korean ? "병합 기록 간 순환 참조가 있어 일괄 재빌드할 수 없습니다." : "Build records contain a dependency cycle and cannot be rebuilt in bulk.";
                case "Rebuild Related Builds": return language == OptimizerLanguage.Japanese ? "関連履歴を一括再統合" : language == OptimizerLanguage.Chinese ? "批量重新合并相关记录" : language == OptimizerLanguage.Korean ? "관련 기록 일괄 재병합" : "Re-merge Related Builds";
                case "Rebuild Related Builds Confirmation": return language == OptimizerLanguage.Japanese ? "対象ルートの検索結果に含まれる統合履歴を、依存順にすべて再ビルドしますか？" : language == OptimizerLanguage.Chinese ? "要按依赖顺序重新构建目标根对象搜索结果中的所有合并记录吗？" : language == OptimizerLanguage.Korean ? "대상 루트 검색 결과에 포함된 모든 병합 기록을 의존 순서대로 다시 빌드할까요?" : "Rebuild all merge records in the target root search results in dependency order?";
                case "Bulk Rebuild Completed": return language == OptimizerLanguage.Japanese ? "一括再ビルド完了" : language == OptimizerLanguage.Chinese ? "批量重新构建完成" : language == OptimizerLanguage.Korean ? "일괄 재빌드 완료" : "Bulk rebuild completed";
                case "Restore Previous Revision": return language == OptimizerLanguage.Japanese ? "1つ前のRevisionへ戻す" : language == OptimizerLanguage.Chinese ? "恢复到上一修订" : language == OptimizerLanguage.Korean ? "이전 리비전으로 복원" : "Restore Previous Revision";
                case "Restore Next Revision": return language == OptimizerLanguage.Japanese ? "次のRevisionへ進む" : language == OptimizerLanguage.Chinese ? "前进到下一修订" : language == OptimizerLanguage.Korean ? "다음 리비전으로 이동" : "Restore Next Revision";
                case "Revision Management": return language == OptimizerLanguage.Japanese ? "Revision管理" : language == OptimizerLanguage.Chinese ? "修订管理" : language == OptimizerLanguage.Korean ? "리비전 관리" : "Revision Management";
                case "Apply Revision": return language == OptimizerLanguage.Japanese ? "適用" : language == OptimizerLanguage.Chinese ? "应用" : language == OptimizerLanguage.Korean ? "적용" : "Apply";
                case "Apply Revision Confirmation": return language == OptimizerLanguage.Japanese ? "統合先Rendererへ選択したRevisionのMesh・Material・Bone設定を適用しますか？SourceObjectは変更されません。" : language == OptimizerLanguage.Chinese ? "要将所选修订的Mesh、Material和Bone设置应用到合并输出Renderer吗？源对象不会改变。" : language == OptimizerLanguage.Korean ? "선택한 리비전의 Mesh, Material, Bone 설정을 병합 출력 Renderer에 적용할까요? 소스 오브젝트는 변경되지 않습니다." : "Apply the selected revision's Mesh, Material and bone settings to the merged Renderer? Source objects will not be changed.";
                case "Restore Previous Revision Confirmation": return language == OptimizerLanguage.Japanese ? "統合先Rendererを1つ前の生成アセットへ戻しますか？SourceObjectは変更されません。" : language == OptimizerLanguage.Chinese ? "要将合并输出Renderer恢复到上一组生成资源吗？源对象不会改变。" : language == OptimizerLanguage.Korean ? "병합 출력 Renderer를 이전 생성 에셋으로 복원할까요? 소스 오브젝트는 변경되지 않습니다." : "Restore the merged Renderer to the previous generated assets? Source objects will not be changed.";
                case "No Previous Revision": return language == OptimizerLanguage.Japanese ? "戻せる旧Revisionがありません。" : language == OptimizerLanguage.Chinese ? "没有可恢复的旧修订。" : language == OptimizerLanguage.Korean ? "복원할 이전 리비전이 없습니다." : "There is no previous revision to restore.";
                case "No Next Revision": return language == OptimizerLanguage.Japanese ? "これより新しいRevisionはありません。" : language == OptimizerLanguage.Chinese ? "没有更新的修订。" : language == OptimizerLanguage.Korean ? "더 새로운 리비전이 없습니다." : "There is no newer revision to restore.";
                case "Revision Restore Failed": return language == OptimizerLanguage.Japanese ? "統合先またはアバタールートを解決できないため、Revisionを復元できません。" : language == OptimizerLanguage.Chinese ? "无法解析合并输出或Avatar根对象，不能恢复修订。" : language == OptimizerLanguage.Korean ? "병합 출력 또는 아바타 루트를 찾을 수 없어 리비전을 복원할 수 없습니다." : "The output or avatar root could not be resolved, so the revision cannot be restored.";
                case "Revision Assets Missing": return language == OptimizerLanguage.Japanese ? "旧RevisionのMeshまたはMaterialが見つかりません。" : language == OptimizerLanguage.Chinese ? "找不到旧修订的Mesh或Material。" : language == OptimizerLanguage.Korean ? "이전 리비전의 Mesh 또는 Material을 찾을 수 없습니다." : "Mesh or Material assets for the previous revision are missing.";
                case "Previous Revision Restored": return language == OptimizerLanguage.Japanese ? "1つ前のRevisionへ戻しました。" : language == OptimizerLanguage.Chinese ? "已恢复到上一修订。" : language == OptimizerLanguage.Korean ? "이전 리비전으로 복원했습니다." : "The previous revision was restored.";
                case "Revision Restored": return language == OptimizerLanguage.Japanese ? "選択したRevisionを適用しました。" : language == OptimizerLanguage.Chinese ? "已应用所选修订。" : language == OptimizerLanguage.Korean ? "선택한 리비전을 적용했습니다." : "The selected revision was applied.";
                case "Generated Assets": return language == OptimizerLanguage.Japanese ? "生成アセット数" : language == OptimizerLanguage.Chinese ? "生成资源数量" : language == OptimizerLanguage.Korean ? "생성 에셋 수" : "Generated Assets";
                case "Select Output": return language == OptimizerLanguage.Japanese ? "統合先を選択" : language == OptimizerLanguage.Chinese ? "选择输出" : language == OptimizerLanguage.Korean ? "출력 선택" : "Select Output";
                case "Select Sources": return language == OptimizerLanguage.Japanese ? "ソースを選択" : language == OptimizerLanguage.Chinese ? "选择源对象" : language == OptimizerLanguage.Korean ? "소스 선택" : "Select Sources";
                case "Restore": return language == OptimizerLanguage.Japanese ? "元に戻す" : language == OptimizerLanguage.Chinese ? "还原" : language == OptimizerLanguage.Korean ? "복원" : "Restore";
                case "Restore Build": return language == OptimizerLanguage.Japanese ? "統合を元に戻す" : language == OptimizerLanguage.Chinese ? "还原合并输出" : language == OptimizerLanguage.Korean ? "병합 출력 복원" : "Restore Merged Output";
                case "Restore Build Confirmation": return language == OptimizerLanguage.Japanese ? "検出できたソースオブジェクトの親・表示状態・タグを復元し、統合先オブジェクトを削除しますか？生成アセットは保持されます。" : language == OptimizerLanguage.Chinese ? "要恢复可找到的源对象原始状态并删除合并输出吗？生成的资源将被保留。" : language == OptimizerLanguage.Korean ? "찾을 수 있는 소스 오브젝트의 원래 상태를 복원하고 병합 출력을 삭제할까요? 생성된 에셋은 유지됩니다." : "Restore all available source objects to their original state and delete the merged output? Generated assets will be kept.";
                case "Remove Record": return language == OptimizerLanguage.Japanese ? "記録のみ削除" : language == OptimizerLanguage.Chinese ? "删除记录" : language == OptimizerLanguage.Korean ? "기록 삭제" : "Remove Record";
                case "Remove Record Confirmation": return language == OptimizerLanguage.Japanese ? "この管理記録だけを削除しますか？シーン上のオブジェクトと生成アセットは変更されません。" : language == OptimizerLanguage.Chinese ? "只删除此管理记录吗？场景对象和生成资源不会改变。" : language == OptimizerLanguage.Korean ? "관리 기록만 삭제할까요? 씬 오브젝트와 생성된 에셋은 변경되지 않습니다." : "Remove only this management record? Scene objects and generated assets will not be changed.";
                case "Cancel": return language == OptimizerLanguage.Japanese ? "キャンセル" : language == OptimizerLanguage.Chinese ? "取消" : language == OptimizerLanguage.Korean ? "취소" : "Cancel";
                case "Status Valid": return language == OptimizerLanguage.Japanese ? "正常" : language == OptimizerLanguage.Chinese ? "正常" : language == OptimizerLanguage.Korean ? "정상" : "Valid";
                case "Status Output Disabled": return language == OptimizerLanguage.Japanese ? "統合先が無効" : language == OptimizerLanguage.Chinese ? "输出已禁用" : language == OptimizerLanguage.Korean ? "출력 비활성" : "Output Disabled";
                case "Status Output Missing": return language == OptimizerLanguage.Japanese ? "統合先なし" : language == OptimizerLanguage.Chinese ? "输出丢失" : language == OptimizerLanguage.Korean ? "출력 없음" : "Output Missing";
                case "Status Source Missing": return language == OptimizerLanguage.Japanese ? "ソース不足" : language == OptimizerLanguage.Chinese ? "源对象丢失" : language == OptimizerLanguage.Korean ? "소스 없음" : "Source Missing";
                case "Status Source Parent Missing": return language == OptimizerLanguage.Japanese ? "元の親が不明" : language == OptimizerLanguage.Chinese ? "原父对象丢失" : language == OptimizerLanguage.Korean ? "원래 부모 없음" : "Original Parent Missing";
                case "Status Modified": return language == OptimizerLanguage.Japanese ? "状態変更あり" : language == OptimizerLanguage.Chinese ? "已修改" : language == OptimizerLanguage.Korean ? "상태 변경됨" : "Modified";
                case "Restore Missing Dependencies": return language == OptimizerLanguage.Japanese ? "復元に必要なオブジェクトが見つからないため、処理を中止しました。統合先と管理記録は変更されていません。" : language == OptimizerLanguage.Chinese ? "找不到还原所需的对象，因此已取消操作。合并输出和管理记录未被更改。" : language == OptimizerLanguage.Korean ? "복원에 필요한 오브젝트를 찾을 수 없어 작업을 중단했습니다. 병합 출력과 관리 기록은 변경되지 않았습니다." : "Restore was cancelled because required objects could not be found. The merged output and management record were not changed.";
                case "Missing Sources": return language == OptimizerLanguage.Japanese ? "見つからないソース" : language == OptimizerLanguage.Chinese ? "丢失的源对象" : language == OptimizerLanguage.Korean ? "누락된 소스" : "Missing sources";
                case "Missing Original Parents": return language == OptimizerLanguage.Japanese ? "見つからない元の親（ソース）" : language == OptimizerLanguage.Chinese ? "丢失的原父对象（源对象）" : language == OptimizerLanguage.Korean ? "누락된 원래 부모(소스)" : "Missing original parents (source)";
                case "Status Renderer Disabled": return language == OptimizerLanguage.Japanese ? "Rendererが無効" : language == OptimizerLanguage.Chinese ? "Renderer已禁用" : language == OptimizerLanguage.Korean ? "Renderer 비활성" : "Renderer Disabled";
                case "Status Scene Not Loaded": return language == OptimizerLanguage.Japanese ? "シーン未ロード" : language == OptimizerLanguage.Chinese ? "场景未加载" : language == OptimizerLanguage.Korean ? "씬 로드 안 됨" : "Scene Not Loaded";
                case "Restore Scene Not Loaded": return language == OptimizerLanguage.Japanese ? "対象オブジェクトを含むシーンが読み込まれていません。該当シーンを開いてから再度実行してください。統合先と管理記録は変更されていません。" : language == OptimizerLanguage.Chinese ? "包含目标对象的场景尚未加载。请打开相应场景后重试。合并输出和管理记录未被更改。" : language == OptimizerLanguage.Korean ? "대상 오브젝트가 포함된 씬이 로드되지 않았습니다. 해당 씬을 연 후 다시 실행하세요. 병합 출력과 관리 기록은 변경되지 않았습니다." : "The scene containing the managed objects is not loaded. Open that scene and try again. The merged output and management record were not changed.";
                case "New Merge": return language == OptimizerLanguage.Japanese ? "新規統合" : language == OptimizerLanguage.Chinese ? "新建合并" : language == OptimizerLanguage.Korean ? "새 병합" : "New Merge";
                case "History and Restore": return language == OptimizerLanguage.Japanese ? "統合履歴・管理" : language == OptimizerLanguage.Chinese ? "历史与还原" : language == OptimizerLanguage.Korean ? "기록 및 복원" : "History & Restore";
                case "Target Selection Method": return language == OptimizerLanguage.Japanese ? "対象の選び方" : language == OptimizerLanguage.Chinese ? "目标选择方式" : language == OptimizerLanguage.Korean ? "대상 선택 방법" : "Target Selection Method";
                case "Create New Merge": return language == OptimizerLanguage.Japanese ? "新しい統合を作成" : language == OptimizerLanguage.Chinese ? "创建新合并" : language == OptimizerLanguage.Korean ? "새 병합 만들기" : "Create New Merge";
                case "History Filter All": return language == OptimizerLanguage.Japanese ? "すべて" : language == OptimizerLanguage.Chinese ? "全部" : language == OptimizerLanguage.Korean ? "전체" : "All";
                case "History Filter Issues": return language == OptimizerLanguage.Japanese ? "問題あり" : language == OptimizerLanguage.Chinese ? "有问题" : language == OptimizerLanguage.Korean ? "문제 있음" : "Issues";
                case "History Group Unmerged Count": return language == OptimizerLanguage.Japanese ? "件未統合" : language == OptimizerLanguage.Chinese ? "项未合并" : language == OptimizerLanguage.Korean ? "개 미병합" : " unmerged";
                case "History Group Merge Complete": return language == OptimizerLanguage.Japanese ? "統合完了" : language == OptimizerLanguage.Chinese ? "合并完成" : language == OptimizerLanguage.Korean ? "병합 완료" : "Merge Complete";
                case "History Filter Valid": return language == OptimizerLanguage.Japanese ? "正常のみ" : language == OptimizerLanguage.Chinese ? "仅正常" : language == OptimizerLanguage.Korean ? "정상만" : "Valid Only";
                case "No Matching Builds": return language == OptimizerLanguage.Japanese ? "条件に一致する履歴はありません。" : language == OptimizerLanguage.Chinese ? "没有符合条件的记录。" : language == OptimizerLanguage.Korean ? "조건에 맞는 기록이 없습니다." : "No build history matches the selected filter.";
                case "Unknown Avatar": return language == OptimizerLanguage.Japanese ? "不明なアバター" : language == OptimizerLanguage.Chinese ? "未知头像" : language == OptimizerLanguage.Korean ? "알 수 없는 아바타" : "Unknown Avatar";
                case "Remove Unrecoverable Record": return language == OptimizerLanguage.Japanese ? "追跡不能記録を削除" : language == OptimizerLanguage.Chinese ? "删除无法恢复的记录" : language == OptimizerLanguage.Korean ? "복구 불가 기록 삭제" : "Remove Unrecoverable Record";
                case "Remove Unrecoverable Record Confirmation": return language == OptimizerLanguage.Japanese ? "Sourceまたは元の親を復元できない記録です。管理記録だけを削除しますか？シーン上のオブジェクトと生成アセットは変更されません。" : language == OptimizerLanguage.Chinese ? "此记录的源对象或原父对象无法恢复。只删除管理记录吗？场景对象和生成资源不会改变。" : language == OptimizerLanguage.Korean ? "소스 또는 원래 부모를 복원할 수 없는 기록입니다. 관리 기록만 삭제할까요? 씬 오브젝트와 생성된 에셋은 변경되지 않습니다." : "The source or original parent for this build cannot be restored. Remove only the management record? Scene objects and generated assets will not be changed.";
                case "Remove All Unrecoverable Records": return language == OptimizerLanguage.Japanese ? "追跡不能記録を一括削除" : language == OptimizerLanguage.Chinese ? "批量删除无法恢复的记录" : language == OptimizerLanguage.Korean ? "복구 불가 기록 일괄 삭제" : "Remove All Unrecoverable Records";
                case "Remove All Unrecoverable Records Confirmation": return language == OptimizerLanguage.Japanese ? "復元できない履歴を一括削除しますか？Scene上のオブジェクトと生成アセットは変更されません。対象件数" : language == OptimizerLanguage.Chinese ? "要批量删除无法恢复的记录吗？场景对象和生成资源不会改变。数量" : language == OptimizerLanguage.Korean ? "복원할 수 없는 기록을 모두 삭제할까요? 씬 오브젝트와 생성된 에셋은 변경되지 않습니다. 대상 수" : "Remove all unrecoverable build records? Scene objects and generated assets will not be changed. Count";
                case "Unrecoverable Record Count": return language == OptimizerLanguage.Japanese ? "追跡不能記録数" : language == OptimizerLanguage.Chinese ? "无法恢复的记录数" : language == OptimizerLanguage.Korean ? "복구 불가 기록 수" : "Unrecoverable records";
                case "Empty Output Cleanup": return language == OptimizerLanguage.Japanese ? "空の統合フォルダを掃除" : language == OptimizerLanguage.Chinese ? "清理空的合并文件夹" : language == OptimizerLanguage.Korean ? "빈 병합 폴더 정리" : "Clean Empty Merge Folders";
                case "Empty Output Cleanup Help": return language == OptimizerLanguage.Japanese ? "不要な空の統合オブジェクトを検出しました。\n内容を確認し、削除する項目を選択して、削除ボタンをクリックしてください。" : language == OptimizerLanguage.Chinese ? "检测到不需要的空合并对象。\n请确认内容，选择要删除的项目，然后点击删除按钮。" : language == OptimizerLanguage.Korean ? "불필요한 빈 병합 오브젝트를 찾았습니다.\n내용을 확인하고 삭제할 항목을 선택한 후 삭제 버튼을 클릭하세요." : "Unused empty merge objects were found.\nReview them, select the items to delete, and click the Delete button.";
                case "Scan Empty Output Folders": return language == OptimizerLanguage.Japanese ? "空の統合フォルダを検索" : language == OptimizerLanguage.Chinese ? "扫描空的合并文件夹" : language == OptimizerLanguage.Korean ? "빈 병합 폴더 검색" : "Scan Empty Merge Folders";
                case "Select Cleanup Root": return language == OptimizerLanguage.Japanese ? "対象ルートを指定してください" : language == OptimizerLanguage.Chinese ? "请指定目标根对象" : language == OptimizerLanguage.Korean ? "대상 루트를 지정하세요" : "Assign a target root";
                case "Empty Output Folders Found": return language == OptimizerLanguage.Japanese ? "検出数" : language == OptimizerLanguage.Chinese ? "检测数量" : language == OptimizerLanguage.Korean ? "검색 결과" : "Found";
                case "No Empty Output Folders": return language == OptimizerLanguage.Japanese ? "空の統合フォルダはありません。" : language == OptimizerLanguage.Chinese ? "没有空的合并文件夹。" : language == OptimizerLanguage.Korean ? "빈 병합 폴더가 없습니다." : "No empty merge folders found.";
                case "Delete Selected Empty Folders": return language == OptimizerLanguage.Japanese ? "チェックしたオブジェクトを削除" : language == OptimizerLanguage.Chinese ? "删除已勾选的对象" : language == OptimizerLanguage.Korean ? "체크한 오브젝트 삭제" : "Delete Checked Objects";
                case "Delete Empty Folders Confirmation Title": return language == OptimizerLanguage.Japanese ? "空の統合フォルダを削除" : language == OptimizerLanguage.Chinese ? "删除空的合并文件夹" : language == OptimizerLanguage.Korean ? "빈 병합 폴더 삭제" : "Delete Empty Merge Folders";
                case "Delete Empty Folders Confirmation": return language == OptimizerLanguage.Japanese ? "チェックした空の__MeshMaterialCombinerを削除しますか？" : language == OptimizerLanguage.Chinese ? "要删除已勾选的空__MeshMaterialCombiner吗？" : language == OptimizerLanguage.Korean ? "체크한 빈 __MeshMaterialCombiner를 삭제할까요?" : "Delete the checked empty __MeshMaterialCombiner containers?";
                case "Empty Folders Deleted": return language == OptimizerLanguage.Japanese ? "削除したフォルダ数" : language == OptimizerLanguage.Chinese ? "已删除文件夹数" : language == OptimizerLanguage.Korean ? "삭제된 폴더 수" : "Folders deleted";
                case "Empty Output Folders Notice": return language == OptimizerLanguage.Japanese ? "使用していないオブジェクトが残っています。削除しますか？ 件数:" : language == OptimizerLanguage.Chinese ? "检测到未使用的对象。要删除吗？数量:" : language == OptimizerLanguage.Korean ? "사용하지 않는 오브젝트가 남아 있습니다. 삭제할까요? 개수:" : "Unused objects remain. Delete them? Count:";
                case "Review Empty Output Folders": return language == OptimizerLanguage.Japanese ? "確認する" : language == OptimizerLanguage.Chinese ? "检查" : language == OptimizerLanguage.Korean ? "확인" : "Review";
                case "Ignore This Time": return language == OptimizerLanguage.Japanese ? "今回は無視" : language == OptimizerLanguage.Chinese ? "本次忽略" : language == OptimizerLanguage.Korean ? "이번에는 무시" : "Ignore This Time";
                default: return null;
            }
        }

        private static string English(string value)
        {
            switch (value)
            {
                case "Preset Safe Description": return "Safe: Materials are not merged. Only duplicate Material Slots that reference the same Material are reduced; UVs and source Material settings are preserved.";
                case "Representative Material": return "Representative Material";
                case "Representative Material Tooltip": return "Material whose Shader and non-atlased settings are used by Force Single Slot. Leave empty to select a supported Material automatically.";
                case "Merged Output Description": return "Merged output is placed under __MeshMaterialCombiner at the avatar root. Selected sources are moved under a hidden, EditorOnly Combined+EditorOnly container. Renderer enabled states are preserved.";
                case "Assign Target Root Help": return "Assign a Target Root to detect SkinnedMeshRenderer and MeshRenderer + MeshFilter components.";
                case "Assign Direct Objects Help": return "Add one or more GameObjects in Direct Objects mode to detect their Renderers.";
                case "Renderer Selection Help": return "Only checked Renderers are included in Preview and Merge. Use Select to locate an object in the Hierarchy.";
                case "Direct Object Help": return "Select or drag a GameObject into the input field to add it automatically. Use Add Selected for multiple Hierarchy selections.";
                case "Readme": return "Readme";
                case "Tool Title": return "Mesh Material Combiner";
                case "Tool Subtitle": return "A non-destructive mesh and material optimization tool for VRChat avatars.";
                default: return value;
            }
        }

        public static string PresetDescription(OptimizationPreset preset)
        {
            if (preset == OptimizationPreset.ForceSingleSlot)
            {
                switch (Language)
                {
                    case OptimizerLanguage.Japanese:
                        return "Force Single Slot（Beta）: すべてのMaterialを代表Shaderの1つのMaterial Slotへ強制統合するBeta機能です。見た目が大きく変わる可能性があります。Triangles以外のTopologyは統合できません。";
                    case OptimizerLanguage.Chinese:
                        return "Force Single Slot（Beta）：这是一项将所有材质强制合并到代表Shader的一个材质槽的Beta功能。外观可能发生明显变化。不支持非Triangles拓扑。";
                    case OptimizerLanguage.Korean:
                        return "Force Single Slot(Beta): 모든 Material을 대표 Shader의 하나의 Material Slot으로 강제 병합하는 Beta 기능입니다. 외관이 크게 달라질 수 있으며 Triangles 이외의 Topology는 지원되지 않습니다.";
                    default:
                        return "Force Single Slot (Beta): A beta feature that forces all materials into one Material Slot using a representative Shader. Visual changes are likely. Non-triangle topology is not supported.";
                }
            }
            preset = OptimizationPresetUtility.Normalize(preset);
            var key = preset == OptimizationPreset.Safe
                ? "Preset Safe Description"
                : "Preset Compatible Merge Description";
            return T(key);
        }

        private static string Japanese(string value)
        {
            switch (value)
            {
                case "Merge Target Mode": return "統合オブジェクト選択モード";
                case "Target Root": return "ルートオブジェクト";
                case "Direct Objects": return "オブジェクト直接選択";
                case "Direct Object Targets": return "個別オブジェクト選択";
                case "Output Settings": return "出力設定";
                case "Merge Settings": return "統合設定";
                case "Output Name": return "出力名";
                case "Output Folder": return "出力フォルダ";
                case "Direct Object Renderers": return "個別オブジェクトのRenderer";
                case "Atlas Resolution": return "Atlas解像度";
                case "BlendShape Collision": return "BlendShape衝突";
                case "Optimization Preset": return "最適化プリセット";
                case "Representative Material": return "代表マテリアル";
                case "Representative Material Tooltip": return "Force Single Slotで使用するShaderと、Atlas化されない設定の基準になるマテリアルです。未指定の場合は対応マテリアルを自動選択します。";
                case "Refresh Renderers": return "Renderer再検出";
                case "Add Selected": return "選択状態のオブジェクトを追加";
                case "Selected Reset": return "選択をリセット";
                case "Remove": return "削除";
                case "Analyze": return "解析";
                case "Merge Meshes": return "メッシュを統合";
                case "Preview": return "プレビュー";
                case "Validation": return "検証";
                case "Scene Preview": return "シーンプレビュー";
                case "Create Scene Preview": return "シーンプレビューを作成";
                case "Exit Preview": return "プレビュー終了";
                case "All ON": return "すべてON";
                case "All OFF": return "すべてOFF";
                case "Preset Safe Description": return "Safe: 異なるMaterialは統合しません。同じMaterialを参照する重複Slotだけを削減し、UVと元Material設定を保持します。";
                case "Merged Output Description": return "統合結果はアバタールート直下の__MeshMaterialCombinerに配置されます。選択したソースは非表示・EditorOnly化されたCombined+EditorOnlyの配下へ移動します。Rendererの有効状態は変更されません。";
                case "Assign Target Root Help": return "Target Rootを指定すると、SkinnedMeshRendererとMeshRenderer + MeshFilterを検出します。";
                case "Assign Direct Objects Help": return "オブジェクト直接選択モードでGameObjectを1つ以上追加すると、Rendererを検出します。";
                case "Renderer Selection Help": return "チェックされたRendererだけがPreviewとMergeの対象です。SelectでHierarchy上のオブジェクトを確認できます。";
                case "Direct Object Help": return "GameObjectを選択または入力欄へドラッグすると自動追加されます。Hierarchyで複数選択する場合は「選択状態のオブジェクトを追加」を使用します。";
                case "Readme": return "使い方";
                case "Tool Title": return "Mesh Material Combiner";
                case "Tool Subtitle": return "VRChatアバター向けのメッシュ・マテリアル最適化ツールです。";
                default: return value;
            }
        }

        private static string Chinese(string value)
        {
            switch (value)
            {
                case "Merge Target Mode": return "合并目标模式";
                case "Target Root": return "目标根对象";
                case "Direct Objects": return "直接对象";
                case "Direct Object Targets": return "直接对象目标";
                case "Output Settings": return "输出设置";
                case "Merge Settings": return "合并设置";
                case "Output Name": return "输出名称";
                case "Output Folder": return "输出文件夹";
                case "Direct Object Renderers": return "直接对象的Renderer";
                case "Optimization Preset": return "优化预设";
                case "Representative Material": return "代表材质";
                case "Representative Material Tooltip": return "Force Single Slot使用此材质的Shader和未图集化设置。留空时自动选择支持的材质。";
                case "Atlas Resolution": return "图集分辨率";
                case "BlendShape Collision": return "BlendShape冲突";
                case "Refresh Renderers": return "重新检测Renderer";
                case "Add Selected": return "添加所选";
                case "Selected Reset": return "重置选择";
                case "All ON": return "全部开启";
                case "All OFF": return "全部关闭";
                case "Analyze": return "分析";
                case "Merge Meshes": return "合并网格";
                case "Preview": return "预览";
                case "Validation": return "验证";
                case "Scene Preview": return "场景预览";
                case "Create Scene Preview": return "创建场景预览";
                case "Exit Preview": return "退出预览";
                case "Readme": return "使用说明";
                case "Remove": return "删除";
                case "Preset Safe Description": return "Safe：不合并不同材质。只减少引用同一材质的重复Slot，并保留UV和原材质设置。";
                case "Merged Output Description": return "合并结果放置在Avatar根对象下的__MeshMaterialCombiner中。选中的源对象会移动到隐藏且标记为EditorOnly的Combined+EditorOnly容器下。不会修改Renderer启用状态。";
                case "Assign Target Root Help": return "指定Target Root以检测SkinnedMeshRenderer和MeshRenderer + MeshFilter。";
                case "Assign Direct Objects Help": return "在直接对象模式中添加一个或多个GameObject即可检测其Renderer。";
                case "Renderer Selection Help": return "只有勾选的Renderer会参与预览和合并。使用Select可在Hierarchy中定位对象。";
                case "Direct Object Help": return "选择或将GameObject拖入输入框即可自动添加。要添加多个Hierarchy对象，请使用添加所选按钮。";
                case "Tool Title": return "Mesh Material Combiner";
                case "Tool Subtitle": return "用于VRChat Avatar的非破坏性网格和材质优化工具。";
                default: return value;
            }
        }

        private static string Korean(string value)
        {
            switch (value)
            {
                case "Merge Target Mode": return "병합 대상 모드";
                case "Target Root": return "대상 루트";
                case "Direct Objects": return "개별 오브젝트";
                case "Direct Object Targets": return "개별 오브젝트 대상";
                case "Output Settings": return "출력 설정";
                case "Merge Settings": return "병합 설정";
                case "Output Name": return "출력 이름";
                case "Output Folder": return "출력 폴더";
                case "Direct Object Renderers": return "개별 오브젝트 Renderer";
                case "Optimization Preset": return "최적화 프리셋";
                case "Representative Material": return "대표 머티리얼";
                case "Representative Material Tooltip": return "Force Single Slot에서 사용할 Shader와 아틀라스화되지 않는 설정의 기준 머티리얼입니다. 비워 두면 지원되는 머티리얼을 자동 선택합니다.";
                case "Atlas Resolution": return "아틀라스 해상도";
                case "BlendShape Collision": return "BlendShape 충돌";
                case "Refresh Renderers": return "Renderer 다시 검색";
                case "Add Selected": return "선택 항목 추가";
                case "Selected Reset": return "선택 초기화";
                case "All ON": return "모두 켜기";
                case "All OFF": return "모두 끄기";
                case "Analyze": return "분석";
                case "Merge Meshes": return "메시 병합";
                case "Preview": return "미리보기";
                case "Validation": return "검증";
                case "Scene Preview": return "씬 미리보기";
                case "Create Scene Preview": return "씬 미리보기 생성";
                case "Exit Preview": return "미리보기 종료";
                case "Readme": return "사용 방법";
                case "Remove": return "삭제";
                case "Preset Safe Description": return "Safe: 서로 다른 머티리얼은 병합하지 않습니다. 같은 머티리얼을 참조하는 중복 Slot만 줄이고 UV와 원본 설정을 유지합니다.";
                case "Merged Output Description": return "병합 결과는 아바타 루트 아래 __MeshMaterialCombiner에 배치됩니다. 선택한 소스는 비활성화되고 EditorOnly 처리된 Combined+EditorOnly 컨테이너 아래로 이동합니다. Renderer 활성 상태는 변경하지 않습니다.";
                case "Assign Target Root Help": return "Target Root를 지정하면 SkinnedMeshRenderer와 MeshRenderer + MeshFilter를 검색합니다.";
                case "Assign Direct Objects Help": return "개별 오브젝트 모드에서 하나 이상의 GameObject를 추가하면 Renderer를 검색합니다.";
                case "Renderer Selection Help": return "체크된 Renderer만 미리보기와 병합 대상이 됩니다. Select로 Hierarchy의 오브젝트를 확인할 수 있습니다.";
                case "Direct Object Help": return "GameObject를 선택하거나 입력 필드로 드래그하면 자동으로 추가됩니다. Hierarchy에서 여러 개를 선택하려면 선택 항목 추가를 사용하세요.";
                case "Tool Title": return "Mesh Material Combiner";
                case "Tool Subtitle": return "VRChat 아바타를 위한 비파괴 메시 및 머티리얼 최적화 도구입니다.";
                default: return value;
            }
        }
    }
}
