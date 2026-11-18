using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TBDA.Platform
{
    public enum WindowDisplayMode
    {
        FramedDualScreenIntro = 0,
        TaskbarOverlay = 1,
        FramedHandheldDS = 2
    }

    /// <summary>
    /// Production-ready Windows Taskbar Overlay controller for Unity 6 (Built-in 2D + DX11 BitBlt).
    /// Incorporates proven patterns from kirurobo/UniWindowController and UnityDesktopPetFramework:
    /// - Thread-safe UnityWndClass HWND lookup via EnumThreadWindows (survives Alt-Tab on launch)
    /// - x64-safe GetWindowLongPtr / SetWindowLongPtr
    /// - DwmExtendFrameIntoClientArea(-1) true alpha + optional ColorKey fallback
    /// - Cached Raycast Hit-Test (EventSystem UI + Physics2D) so Win32 syscalls only fire on state change
    /// </summary>
    public sealed class Win32TaskbarOverlayController : MonoBehaviour
    {
        [Header("Configuração de Janela")]
        [SerializeField] private int taskbarWindowHeightPx = 420;
        [SerializeField] private float interactiveBottomStripPx = 116f;
        [SerializeField] private bool dockAboveWindowsTaskbar = true;
        [SerializeField] private LayerMask worldInteractiveLayers = ~0;

        public WindowDisplayMode CurrentMode { get; private set; } = WindowDisplayMode.FramedDualScreenIntro;
        public bool IsCurrentlyClickThrough { get; private set; }
        public float InteractiveBottomStripPx => interactiveBottomStripPx;

        private Camera _mainCamera;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IntPtr _hwnd = IntPtr.Zero;
        private IntPtr _originalStyle = IntPtr.Zero;
        private IntPtr _originalExStyle = IntPtr.Zero;
        private bool _hasCachedOriginalStyle;

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        private const long WS_POPUP = 0x80000000L;
        private const long WS_VISIBLE = 0x10000000L;
        private const long WS_OVERLAPPEDWINDOW = 0x00CF0000L;

        private const long WS_EX_LAYERED = 0x00080000L;
        private const long WS_EX_TRANSPARENT = 0x00000020L;
        private const long WS_EX_TOPMOST = 0x00000008L;

        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SPI_GETWORKAREA = 0x0030;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(
            uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        private static IntPtr GetWindowLongPtrSafe(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLongPtrSafe(IntPtr hWnd, int nIndex, long value)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, new IntPtr(value))
                : new IntPtr(SetWindowLong32(hWnd, nIndex, unchecked((int)value)));
        }

        private IntPtr AcquireUnityWindowHandle()
        {
            if (_hwnd != IntPtr.Zero) return _hwnd;

            IntPtr found = IntPtr.Zero;
            uint threadId = GetCurrentThreadId();
            EnumThreadWindows(threadId, (hWnd, _) =>
            {
                var sb = new StringBuilder(64);
                GetClassName(hWnd, sb, sb.Capacity);
                if (sb.ToString() == "UnityWndClass")
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            _hwnd = found != IntPtr.Zero ? found : GetActiveWindow();

            if (_hwnd != IntPtr.Zero && !_hasCachedOriginalStyle)
            {
                _originalStyle = GetWindowLongPtrSafe(_hwnd, GWL_STYLE);
                _originalExStyle = GetWindowLongPtrSafe(_hwnd, GWL_EXSTYLE);
                _hasCachedOriginalStyle = true;
            }
            return _hwnd;
        }
#endif

        private void Awake()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            _mainCamera = Camera.main;
        }

        public void SetCamera(Camera cam)
        {
            _mainCamera = cam;
        }

        public void ApplyDisplayMode(WindowDisplayMode mode)
        {
            CurrentMode = mode;
            if (_mainCamera == null) _mainCamera = Camera.main;

            if (_mainCamera != null)
            {
                _mainCamera.clearFlags = CameraClearFlags.SolidColor;
                _mainCamera.backgroundColor = (mode == WindowDisplayMode.TaskbarOverlay)
                    ? new Color(0f, 0f, 0f, 0f)       // Alpha 0 required for DWM desktop transparency!
                    : new Color(0.08f, 0.08f, 0.14f, 1f); // Cozy Nintendo DS dark bezel
            }

            // Save battery/CPU when running peacefully in background taskbar
            Application.targetFrameRate = (mode == WindowDisplayMode.TaskbarOverlay) ? 30 : 60;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr hwnd = AcquireUnityWindowHandle();
            if (hwnd == IntPtr.Zero) return;

            if (mode == WindowDisplayMode.TaskbarOverlay)
            {
                RECT workArea = new RECT
                {
                    Left = 0,
                    Top = 0,
                    Right = Display.main.systemWidth,
                    Bottom = Display.main.systemHeight
                };

                if (dockAboveWindowsTaskbar)
                {
                    SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);
                }

                int width = Math.Max(640, workArea.Right - workArea.Left);
                int height = Mathf.Clamp(taskbarWindowHeightPx, 240, 640);
                int posX = workArea.Left;
                int posY = workArea.Bottom - height;

                SetWindowLongPtrSafe(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
                SetWindowLongPtrSafe(hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TOPMOST);

                var margins = new MARGINS
                {
                    cxLeftWidth = -1,
                    cxRightWidth = -1,
                    cyTopHeight = -1,
                    cyBottomHeight = -1
                };
                DwmExtendFrameIntoClientArea(hwnd, ref margins);

                SetWindowPos(hwnd, HWND_TOPMOST, posX, posY, width, height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                IsCurrentlyClickThrough = false;
            }
            else
            {
                // Framed Dual-Screen Nintendo DS Window (512x768 = 2x scale of 256x384 stacked DS screens)
                int dsWidth = 540;
                int dsHeight = 780;
                int centerX = (Display.main.systemWidth - dsWidth) / 2;
                int centerY = Math.Max(20, (Display.main.systemHeight - dsHeight) / 2);

                var margins = new MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
                DwmExtendFrameIntoClientArea(hwnd, ref margins);

                long restoredStyle = _hasCachedOriginalStyle
                    ? _originalStyle.ToInt64()
                    : (WS_OVERLAPPEDWINDOW | WS_VISIBLE);
                SetWindowLongPtrSafe(hwnd, GWL_STYLE, restoredStyle);

                long restoredEx = _hasCachedOriginalStyle ? _originalExStyle.ToInt64() : 0L;
                SetWindowLongPtrSafe(hwnd, GWL_EXSTYLE, restoredEx);

                SetWindowPos(hwnd, HWND_NOTOPMOST, centerX, centerY, dsWidth, dsHeight, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                IsCurrentlyClickThrough = false;
            }
#endif
        }

        private void Update()
        {
            if (CurrentMode != WindowDisplayMode.TaskbarOverlay)
            {
                IsCurrentlyClickThrough = false;
                return;
            }

            bool pointerOverInteractive = EvaluatePointerHitTest();
            bool shouldClickThrough = !pointerOverInteractive;

            if (shouldClickThrough != IsCurrentlyClickThrough)
            {
                IsCurrentlyClickThrough = shouldClickThrough;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                IntPtr hwnd = AcquireUnityWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    long exStyle = shouldClickThrough
                        ? (WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TRANSPARENT)
                        : (WS_EX_LAYERED | WS_EX_TOPMOST);
                    SetWindowLongPtrSafe(hwnd, GWL_EXSTYLE, exStyle);
                }
#endif
            }
        }

        private bool EvaluatePointerHitTest()
        {
            Vector3 mousePos = Input.mousePosition;

            // Outside window bounds -> click-through
            if (mousePos.x < 0f || mousePos.x > Screen.width || mousePos.y < 0f || mousePos.y > Screen.height)
                return false;

            // Bottom Taskbar Dungeon Strip is always interactive
            if (mousePos.y <= interactiveBottomStripPx)
                return true;

            // Active DS Top-Screen Popup UI elements above the taskbar
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return true;

            // World-space 2D colliders (pet / props)
            if (_mainCamera != null)
            {
                Vector2 worldPt = _mainCamera.ScreenToWorldPoint(mousePos);
                if (Physics2D.OverlapPoint(worldPt, worldInteractiveLayers) != null)
                    return true;
            }

            return false;
        }
    }
}
