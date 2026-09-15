using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>Menu ▸ Nhập code: one field and one button. The code is checked by
    /// <see cref="GiftCodes"/>; the reward card is <see cref="GameApp.ShowReward"/>'s.</summary>
    public class CodePanel : PanelBase
    {
        public CodePanel(GameApp app) : base(app) { }
        public override string Title => "Nhập mã quà tặng";
        public override Vector2 Size => new Vector2(640, 360);

        InputField _input;

        public override void Build()
        {
            var hint = UIKit.Label(Body, "Nhập mã được tặng để nhận quà. Mỗi mã chỉ dùng được một lần trên nông trại này.", 18, Theme.InkSoft, TextAnchor.UpperCenter);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.rectTransform.Anchor(UIKit.Top, new Vector2(0, -6), new Vector2(560, 56));

            // --- the field: a recessed well, dark ink, a grey placeholder ---
            var box = UIKit.Node("field", Body);
            box.Anchor(UIKit.Top, new Vector2(0, -78), new Vector2(520, 72));
            SurfaceLook.Add(box, Looks.Well, 22f);
            var hit = box.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);

            var text = UIKit.Label(box, "", 30, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.supportRichText = false;
            text.rectTransform.Stretch(24, 0, 24, 2);
            var placeholder = UIKit.Label(box, "Nhập mã tại đây", 24, Theme.InkSoft.Alpha(0.5f), TextAnchor.MiddleCenter);
            placeholder.rectTransform.Stretch(24, 0, 24, 2);

            _input = box.gameObject.AddComponent<InputField>();
            _input.targetGraphic = hit;
            _input.textComponent = text;
            _input.placeholder = placeholder;
            _input.characterLimit = 32;
            _input.lineType = InputField.LineType.SingleLine;
            _input.contentType = InputField.ContentType.Alphanumeric;
            _input.caretColor = Theme.Ink;
            _input.customCaretColor = true;
            _input.selectionColor = Theme.Green.Alpha(0.35f);
            _input.shouldHideMobileInput = false;
            _input.onEndEdit.AddListener(v =>
            {
                // the Done / Enter key submits; tapping away just leaves the field
                #if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Submit();
                #endif
                if (_input.touchScreenKeyboard != null && _input.touchScreenKeyboard.status == TouchScreenKeyboard.Status.Done) Submit();
            });

            var go = UIKit.Btn(Body, "Nhận quà", Theme.Green, Theme.GreenDark, 24, 24, Submit);
            go.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 8), new Vector2(260, 60));
        }

        void Submit()
        {
            if (_input == null) return;
            app.RedeemCode(_input.text);
        }

        /// <summary>For the screenshot pass.</summary>
        public void TypeForAudit(string code) { if (_input != null) _input.text = code; }
    }
}
