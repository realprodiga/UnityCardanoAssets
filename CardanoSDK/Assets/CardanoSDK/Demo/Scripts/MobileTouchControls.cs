using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Prodiga.CardanoSDK.Demo
{
    /// <summary>
    /// Twin-stick touch controls for PlayerController, for phones and tablets (including in-wallet
    /// dApp browsers). Attach next to PlayerController on the player.
    ///
    ///   - Left part of the screen: a floating joystick appears where the finger lands and walks
    ///     the player (analog -- a small push walks slowly).
    ///   - Right part of the screen: drag to look around (same as left-click orbit on desktop).
    ///
    /// Only shown on mobile platforms (phone/tablet browsers); desktop players keep the normal
    /// keyboard/mouse controls, unless ShowOnDesktop is on for testing. Builds its own overlay Canvas at
    /// runtime with procedurally drawn circles, so it needs no sprites or prefabs. Touches that start
    /// on other UI (e.g. the Connect Wallet button) are left alone.
    ///
    /// Input reaches PlayerController through ExternalMoveInput / ExternalLookDelta, so a custom
    /// touch UI can drive the controller the same way.
    /// </summary>
    [DefaultExecutionOrder(-50)] // run before PlayerController.Update so input lands the same frame
    [RequireComponent(typeof(PlayerController))]
    public class MobileTouchControls : MonoBehaviour
    {
        [Tooltip("Show the controls with mouse input on desktop too (for testing in the Editor). The mouse then acts as a single touch.")]
        public bool ShowOnDesktop = false;

        [Header("Joystick")]
        [Tooltip("Fraction of the screen width, from the left, where a touch starts the joystick. Touches to the right of it look around.")]
        [Range(0.2f, 0.8f)] public float JoystickAreaWidth = 0.45f;
        [Tooltip("Joystick radius as a fraction of the screen's shorter side.")]
        public float JoystickRadius = 0.11f;
        public Color BaseColor = new Color(1f, 1f, 1f, 0.18f);
        public Color KnobColor = new Color(1f, 1f, 1f, 0.45f);

        [Header("Look")]
        [Tooltip("Degrees of camera rotation per screen-height of drag.")]
        public float LookSensitivity = 180f;
        public bool InvertY = false;

        private PlayerController _player;
        private bool _active;

        private RectTransform _base;
        private RectTransform _knob;
        private int _moveFinger = -1;
        private int _lookFinger = -1;
        private Vector2 _moveOrigin;
        private Vector2 _lastLookPos;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
        }

        void Start()
        {
            // Mobile only: on WebGL, isMobilePlatform reflects a phone/tablet browser (including in-wallet
            // dApp browsers). Touchscreen laptops deliberately keep the desktop keyboard/mouse controls.
            _active = Application.isMobilePlatform || ShowOnDesktop;
            if (!_active) return;

            // Keep touches from also arriving as mouse clicks, which would trigger PlayerController's
            // left-click orbit and cursor lock. Unity UI still receives touches directly.
            Input.simulateMouseWithTouches = false;
            BuildUi();
        }

        void Update()
        {
            if (!_active) return;

            Vector2 move = Vector2.zero;
            Vector2 look = Vector2.zero;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    HandlePointer(t.fingerId, t.phase, t.position, ref move, ref look);
                }
            }
            else if (ShowOnDesktop && !Application.isMobilePlatform)
            {
                TouchPhase phase = Input.GetMouseButtonDown(0) ? TouchPhase.Began
                    : Input.GetMouseButtonUp(0) ? TouchPhase.Ended
                    : Input.GetMouseButton(0) ? TouchPhase.Moved
                    : TouchPhase.Canceled;
                if (phase != TouchPhase.Canceled || _moveFinger == 0 || _lookFinger == 0)
                    HandlePointer(0, phase, Input.mousePosition, ref move, ref look);
            }

            _player.ExternalMoveInput = move;
            _player.ExternalLookDelta += look;
        }

        void OnDisable()
        {
            if (_player != null) _player.ExternalMoveInput = Vector2.zero;
            if (_base != null) _base.gameObject.SetActive(false);
            _moveFinger = _lookFinger = -1;
        }

        private void HandlePointer(int finger, TouchPhase phase, Vector2 pos, ref Vector2 move, ref Vector2 look)
        {
            float radiusPx = JoystickRadius * Mathf.Min(Screen.width, Screen.height);

            if (phase == TouchPhase.Began)
            {
                if (IsOverOtherUi(finger)) return;

                if (_moveFinger < 0 && pos.x < Screen.width * JoystickAreaWidth)
                {
                    _moveFinger = finger;
                    _moveOrigin = pos;
                    ShowJoystick(pos, radiusPx);
                }
                else if (_lookFinger < 0)
                {
                    _lookFinger = finger;
                    _lastLookPos = pos;
                }
                return;
            }

            bool ended = phase == TouchPhase.Ended || phase == TouchPhase.Canceled;

            if (finger == _moveFinger)
            {
                if (ended)
                {
                    _moveFinger = -1;
                    _base.gameObject.SetActive(false);
                    return;
                }
                Vector2 offset = Vector2.ClampMagnitude(pos - _moveOrigin, radiusPx);
                _knob.anchoredPosition = offset;
                move = offset / radiusPx;
            }
            else if (finger == _lookFinger)
            {
                if (ended)
                {
                    _lookFinger = -1;
                    return;
                }
                Vector2 delta = (pos - _lastLookPos) / Screen.height * LookSensitivity;
                _lastLookPos = pos;
                look += new Vector2(delta.x, InvertY ? delta.y : -delta.y);
            }
        }

        private static bool IsOverOtherUi(int finger)
        {
            if (EventSystem.current == null) return false;
            return Input.touchCount > 0
                ? EventSystem.current.IsPointerOverGameObject(finger)
                : EventSystem.current.IsPointerOverGameObject();
        }

        // ---------------------------------------------------------------- UI

        private void BuildUi()
        {
            var canvasObj = new GameObject("MobileTouchControls (runtime)");
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            Sprite circle = MakeCircleSprite(128);

            _base = MakeCircle("JoystickBase", canvasObj.transform, circle, BaseColor);
            _base.anchorMin = _base.anchorMax = Vector2.zero; // bottom-left, so anchoredPosition = screen pixels
            _knob = MakeCircle("JoystickKnob", _base, circle, KnobColor);
            _knob.anchorMin = _knob.anchorMax = new Vector2(0.5f, 0.5f);
            _base.gameObject.SetActive(false);
        }

        private void ShowJoystick(Vector2 screenPos, float radiusPx)
        {
            _base.sizeDelta = Vector2.one * radiusPx * 2f;
            _knob.sizeDelta = Vector2.one * radiusPx * 0.9f;
            _base.anchoredPosition = screenPos;
            _knob.anchoredPosition = Vector2.zero;
            _base.gameObject.SetActive(true);
        }

        private static RectTransform MakeCircle(string name, Transform parent, Sprite sprite, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false; // never block the scene's own UI
            return (RectTransform)obj.transform;
        }

        private static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f); // 1px anti-aliased edge
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
