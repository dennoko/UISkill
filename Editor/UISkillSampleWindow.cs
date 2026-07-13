using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace dennokoworks.UISkill
{
    public class UISkillSampleWindow : EditorWindow
    {
        // 配置した UXML / USS の .meta ファイルに記載されている GUID を設定する
        private const string UXML_GUID = "f22915893a824fbcbc0d3a1d340c1e69";
        private const string USS_GUID  = "676e862255b64334a71f7653e4e652a7";

        public enum StatusType { Info, Success, Error }

        private Label _statusLabel;
        private IVisualElementScheduledItem _statusResetSchedule;

        [MenuItem("Tools/dennokoworks/UI Skill Sample")]
        public static void ShowWindow()
        {
            var window = GetWindow<UISkillSampleWindow>();
            window.titleContent = new GUIContent("UI Skill Sample");
            window.minSize = new Vector2(400, 600);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            // テーマ非依存のためのルートクラスを適用
            root.AddToClassList("dennoko-root");
            // USS ロード失敗時も背景が明るくならないよう Surface0 を C# 側でも保証
            // (StyleColor への暗黙変換は Color のみ。Color32 のままでは CS0029 になる)
            root.style.backgroundColor = (Color)new Color32(0x12, 0x12, 0x12, 0xFF);
            root.style.flexGrow = 1;

            // USS のロードと適用
            string ussPath = AssetDatabase.GUIDToAssetPath(USS_GUID);
            var uss = string.IsNullOrEmpty(ussPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (uss != null)
            {
                root.styleSheets.Add(uss);
            }
            else
            {
                Debug.LogWarning($"[{nameof(UISkillSampleWindow)}] USS が見つかりません。GUID を確認してください: {USS_GUID}");
            }

            // UXML のロードとインスタンス化
            string uxmlPath = AssetDatabase.GUIDToAssetPath(UXML_GUID);
            var uxml = string.IsNullOrEmpty(uxmlPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (uxml == null)
            {
                root.Add(new Label("UXML Asset が見つかりません。GUID を確認してください。"));
                return;
            }
            uxml.CloneTree(root);

            InitializeUI(root);
        }

        // ─── バインディング ─────────────────────────────────────────────

        private void InitializeUI(VisualElement root)
        {
            _statusLabel = root.Q<Label>("status-label");

            // ヘッダー: 言語切り替え (排他アクティブ)
            BindExclusiveButtons(root, "lang-ja-button", "lang-en-button");

            // ボタン: 無効状態のサンプル
            root.Q<Button>("disabled-button").SetEnabled(false);

            // ツールバー: 排他アクティブ切り替え
            BindExclusiveButtons(root, "tool-a-button", "tool-b-button", "tool-c-button");

            // 入力フィールド: DropdownField の選択肢はロジック側で与える
            var dropdown = root.Q<DropdownField>("quality-dropdown");
            dropdown.choices = new List<string> { "Low", "Medium", "High", "Ultra" };
            dropdown.index = 2;

            // トグル付きセクション: トグル OFF でコンテンツをグレーアウト
            BindToggleSection(root,
                toggleName:  "color-correction-toggle",
                contentName: "color-correction-content",
                resetName:   "color-correction-reset",
                onReset: () => ResetColorCorrection(root));

            // ステータスバーのデモ
            root.Q<Button>("status-info-button").clicked +=
                () => SetStatus("これは情報メッセージです。", StatusType.Info);
            root.Q<Button>("status-success-button").clicked +=
                () => SetStatus("処理が完了しました。", StatusType.Success);
            root.Q<Button>("status-error-button").clicked +=
                () => SetStatus("処理に失敗しました。", StatusType.Error);

            root.Q<Button>("apply-button").clicked += ApplyAndSave;
            root.Q<Button>("reset-all-button").clicked += ResetAll;
        }

        /// <summary>トグル・コンテンツ・Reset ボタンを接続する共通ヘルパー。</summary>
        private static void BindToggleSection(
            VisualElement root, string toggleName, string contentName,
            string resetName, System.Action onReset)
        {
            var toggle  = root.Q<Toggle>(toggleName);
            var content = root.Q<VisualElement>(contentName);

            toggle.RegisterValueChangedCallback(evt => content.SetEnabled(evt.newValue));
            content.SetEnabled(toggle.value);

            var reset = root.Q<Button>(resetName);
            if (reset != null && onReset != null)
                reset.clicked += () => onReset();
        }

        /// <summary>複数ボタンのうち押した 1 つだけを dennoko-button-active にするヘルパー。</summary>
        private static void BindExclusiveButtons(VisualElement root, params string[] buttonNames)
        {
            var buttons = new List<Button>();
            foreach (string name in buttonNames)
                buttons.Add(root.Q<Button>(name));

            foreach (var button in buttons)
            {
                var self = button;
                self.clicked += () =>
                {
                    foreach (var other in buttons)
                        other.EnableInClassList("dennoko-button-active", other == self);
                };
            }
        }

        // ─── ステータスバー ─────────────────────────────────────────────

        /// <summary>ステータスを表示する。Success / Error は 3 秒後に Ready へ自動復帰。</summary>
        private void SetStatus(string message, StatusType type, long autoResetMs = 3000)
        {
            if (_statusLabel == null) return; // UXML ロード失敗時・要素名変更時の NRE 防止

            _statusLabel.text = message;
            _statusLabel.EnableInClassList("dennoko-status--success", type == StatusType.Success);
            _statusLabel.EnableInClassList("dennoko-status--error",   type == StatusType.Error);

            _statusResetSchedule?.Pause();
            if (type != StatusType.Info)
            {
                _statusResetSchedule = _statusLabel.schedule
                    .Execute(() => SetStatus("Ready", StatusType.Info))
                    .StartingIn(autoResetMs);
            }
        }

        // ─── アクション ─────────────────────────────────────────────────

        private static void ResetColorCorrection(VisualElement root)
        {
            root.Q<Slider>("hue-slider").value = 0f;
            root.Q<Slider>("sat-slider").value = 1f;
            root.Q<SliderInt>("steps-slider").value = 4;
            root.Q<MinMaxSlider>("range-slider").value = new Vector2(20f, 80f);
        }

        private void ApplyAndSave()
        {
            // サンプルのため保存処理はなし
            SetStatus("Saved.", StatusType.Success);
        }

        private void ResetAll()
        {
            var root = rootVisualElement;
            root.Q<TextField>("name-field").value = "Dennoko";
            root.Q<IntegerField>("count-field").value = 4;
            root.Q<FloatField>("scale-field").value = 1.5f;
            root.Q<DropdownField>("quality-dropdown").index = 2;
            root.Q<Toggle>("enabled-toggle").value = true;
            root.Q<Toggle>("color-correction-toggle").value = true;
            ResetColorCorrection(root);
            SetStatus("Reset.", StatusType.Info);
        }
    }
}
