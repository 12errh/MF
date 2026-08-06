using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Mate.Platform
{
    /// <summary>
    /// X11 window backend, ported from the reference engine's WindowManager.cs
    /// and decoupled from its god-object (SaveLoadHandler), Wayland (Hyprland/
    /// KWin), and DBus dependencies. Provides the window operations the
    /// framework's IWindowService needs: position/size, always-on-top,
    /// borderless, click-through, hide-from-taskbar, window type, mouse
    /// position, monitor enumeration, and window discovery.
    ///
    /// Safe to construct in EditMode tests: if no X display is reachable,
    /// Initialize returns false and all operations return false/empty.
    /// </summary>
    public class X11WindowBackend : IWindowBackend
    {
        private IntPtr _display;
        private IntPtr _rootWindow;
        private IntPtr _unityWindow;

        private IntPtr _netWmState, _netWmStateAbove, _netWmStateSkipTaskbar;
        private IntPtr _netWmWindowType, _netWmWindowTypeDock, _netWmWindowTypeDesktop, _netWmWindowTypeNormal;
        private IntPtr _netMoveResizeWindow;
        private IntPtr _motifHintsAtom;

        private global::System.Threading.Thread _shapingThread;
        private volatile bool _shapingRunning;
        private volatile bool _shapingRequested;
        private volatile bool _closing;

        public IntPtr Display => _display;
        public IntPtr UnityWindow => _unityWindow;

        private const string LibX11 = "libX11.so.6";
        private const string LibXExt = "libXext.so.6";
        private const string LibXRandR = "libXrandr.so.2";
        private const string LibXRender = "libXrender.so.1";
        private const string LibXDamage = "libXdamage.so.1";
        private const string LibXComposite = "libXcomposite.so.1";

        // Atom / mask / event constants
        private const int XaCardinal = 6;
        private const int XaWindow = 33;
        private const int XaAtom = 4;
        private const int IsViewable = 2;
        private const int ClientMessage = 33;
        private const int ConfigureNotify = 22;
        private const int DestroyNotify = 17;
        private const int EnterNotify = 7;
        private const int LeaveNotify = 8;
        private const int ShapeBounding = 0;
        private const int ShapeInput = 2;
        private const int ShapeSet = 0;
        private const int Unsorted = 0;
        private const int YSorted = 1;
        private const int ZPixmap = 2;
        private const int PictTypeDirect = 1;
        private const int XDamageReportNonEmpty = 3;
        private const int CompositeRedirectAutomatic = 0;
        private const int PropModeReplace = 0;
        private const long StructureNotifyMask = 1L << 17;
        private const long SubstructureRedirectMask = 0x00080000;
        private const long SubstructureNotifyMask = 0x00040000;
        private const long EnterWindowMask = 1L << 4;
        private const long LeaveWindowMask = 1L << 5;
        private const long PropertyChangeMask = 1L << 22;
        private const long MwmHintsFlags = 1L << 1;
        private const long MwmDecorationsNone = 0;
        private const ulong AllPlanes = 0xFFFFFFFFFFFFFFFFUL;
        private const int ShapeThreshold = 10; // alpha > 10 = opaque for click-through

        public bool Initialize(IntPtr unityWindow)
        {
            if (_display != IntPtr.Zero)
            {
                _unityWindow = unityWindow != IntPtr.Zero ? unityWindow : _unityWindow;
                return true;
            }

            _display = XOpenDisplay(null);
            if (_display == IntPtr.Zero)
            {
                Debug.LogWarning("[X11WindowBackend] Cannot open X11 display.");
                return false;
            }

            _rootWindow = XDefaultRootWindow(_display);
            _netWmState = XInternAtom(_display, "_NET_WM_STATE", false);
            _netWmStateAbove = XInternAtom(_display, "_NET_WM_STATE_ABOVE", false);
            _netWmStateSkipTaskbar = XInternAtom(_display, "_NET_WM_STATE_SKIP_TASKBAR", false);
            _netWmWindowType = XInternAtom(_display, "_NET_WM_WINDOW_TYPE", false);
            _netWmWindowTypeDock = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DOCK", false);
            _netWmWindowTypeDesktop = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DESKTOP", false);
            _netWmWindowTypeNormal = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_NORMAL", false);
            _netMoveResizeWindow = XInternAtom(_display, "_NET_MOVERESIZE_WINDOW", false);
            _motifHintsAtom = XInternAtom(_display, "_MOTIF_WM_HINTS", false);

            // Locate the Unity window by PID when the caller cannot supply a handle
            // (e.g. bootstrap runs before the player window is enumerated by Unity).
            if (unityWindow == IntPtr.Zero)
            {
                var pid = global::System.Diagnostics.Process.GetCurrentProcess().Id;
                var windows = FindWindowsByPid(pid);
                _unityWindow = windows.Count > 0 ? windows[0] : IntPtr.Zero;
            }
            else
            {
                _unityWindow = unityWindow;
            }
            return true;
        }

        // ---- Position ----

        public bool GetWindowPosition(out Vector2Int position)
        {
            position = Vector2Int.zero;
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return false;
            if (!XTranslateCoordinates(_display, _unityWindow, _rootWindow, 0, 0, out var x, out var y, out _))
                return false;
            position = new Vector2Int(x, y);
            return true;
        }

        public bool SetWindowPosition(Vector2Int position)
        {
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return false;
            if (_netMoveResizeWindow == IntPtr.Zero)
            {
                XMoveWindow(_display, _unityWindow, position.x, position.y);
                XFlush(_display);
                return true;
            }

            var ev = new XClientMessageEvent
            {
                type = ClientMessage,
                window = _unityWindow,
                message_type = _netMoveResizeWindow,
                format = 32,
                data0 = new IntPtr((1 << 12) | (1 << 9) | (1 << 8) | 10),
                data1 = new IntPtr(position.x),
                data2 = new IntPtr(position.y),
            };
            XSendEvent(_display, _rootWindow, false, SubstructureRedirectMask | SubstructureNotifyMask, ref ev);
            XFlush(_display);
            return true;
        }

        public bool GetWindowSize(out Vector2Int size)
        {
            size = Vector2Int.zero;
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return false;
            if (XGetWindowAttributes(_display, _unityWindow, out var attrs) == 0) return false;
            size = new Vector2Int(attrs.width, attrs.height);
            return true;
        }

        public bool SetWindowSize(Vector2Int size)
        {
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return false;
            if (_netMoveResizeWindow == IntPtr.Zero)
            {
                XResizeWindow(_display, _unityWindow, size.x, size.y);
                XFlush(_display);
                return true;
            }

            var ev = new XClientMessageEvent
            {
                type = ClientMessage,
                window = _unityWindow,
                message_type = _netMoveResizeWindow,
                format = 32,
                data0 = new IntPtr((1 << 12) | (1 << 10) | (1 << 11)),
                data3 = new IntPtr(size.x),
                data4 = new IntPtr(size.y),
            };
            XSendEvent(_display, _rootWindow, false, SubstructureRedirectMask | SubstructureNotifyMask, ref ev);
            XFlush(_display);
            return true;
        }

        // ---- State ----

        public bool SetAlwaysOnTop(bool value)
        {
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return false;
            if (_netWmState == IntPtr.Zero || _netWmStateAbove == IntPtr.Zero) return false;

            var ev = new XClientMessageEvent
            {
                type = ClientMessage,
                window = _unityWindow,
                message_type = _netWmState,
                format = 32,
                data0 = new IntPtr(value ? 1 : 0), // 1 = ADD, 0 = REMOVE
                data1 = _netWmStateAbove,
            };
            XSendEvent(_display, _rootWindow, false, SubstructureRedirectMask | SubstructureNotifyMask, ref ev);
            XFlush(_display);
            return true;
        }

        public bool HideFromTaskbar(bool value)
        {
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return false;
            if (_netWmState == IntPtr.Zero || _netWmStateSkipTaskbar == IntPtr.Zero) return false;

            var ev = new XClientMessageEvent
            {
                type = ClientMessage,
                window = _unityWindow,
                message_type = _netWmState,
                format = 32,
                data0 = new IntPtr(value ? 1 : 0),
                data1 = _netWmStateSkipTaskbar,
                data4 = new IntPtr(1),
            };
            XSendEvent(_display, _rootWindow, false, SubstructureRedirectMask | SubstructureNotifyMask, ref ev);
            XFlush(_display);
            return true;
        }

        public bool SetBorderless(bool value)
        {
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return false;

            var hints = new XMotifWmHints
            {
                flags = new IntPtr(MwmHintsFlags),
                decorations = new IntPtr(value ? MwmDecorationsNone : 1),
            };
            ChangeProperty(_motifHintsAtom, _motifHintsAtom, 32, PropModeReplace, hints, 5);
            XFlush(_display);
            return true;
        }

        public bool SetWindowType(int type)
        {
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return false;
            IntPtr atom = type switch
            {
                1 => _netWmWindowTypeDock,
                2 => _netWmWindowTypeDesktop,
                _ => _netWmWindowTypeNormal,
            };
            if (atom == IntPtr.Zero) return false;
            ChangeProperty(_netWmWindowType, (IntPtr)XaAtom, 32, PropModeReplace, atom, 1);
            XFlush(_display);
            return true;
        }

        // ---- Click-through (input shaping) ----

        public bool SetClickThrough(bool value)
        {
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return false;
            if (value)
            {
                // Reset to a full bounding shape first so input works immediately.
                var fullRect = new[] { new XRectangle { Width = (ushort)Screen.width, Height = (ushort)Screen.height } };
                XShapeCombineRectangles(_display, _unityWindow, ShapeInput, 0, 0, fullRect, 1, ShapeSet, Unsorted);
                _shapingRequested = true;
                if (_shapingRunning) return true;

                _shapingRunning = true;
                _shapingThread = new global::System.Threading.Thread(ShapingLoop)
                {
                    Name = "MateShapeThread",
                    IsBackground = true
                };
                _shapingThread.Start();
                return true;
            }
            else
            {
                // Stop the shaping thread and restore a full input rectangle so the
                // window accepts input everywhere.
                _shapingRequested = false;
                _shapingRunning = false;
                if (_shapingThread is { IsAlive: true })
                    _shapingThread.Join(500);
                _shapingThread = null;

                var fullRect = new[] { new XRectangle { X = 0, Y = 0, Width = ushort.MaxValue, Height = ushort.MaxValue } };
                XShapeCombineRectangles(_display, _unityWindow, ShapeInput, 0, 0, fullRect, 1, ShapeSet, Unsorted);
                XFlush(_display);
                return true;
            }
        }

        private void ShapingLoop()
        {
            try
            {
                while (_shapingRunning && !_closing)
                {
                    if (_shapingRequested)
                    {
                        _shapingRequested = false;
                        UpdateInputShape();
                    }
                    global::System.Threading.Thread.Sleep(50);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void UpdateInputShape()
        {
            if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return;
            if (XGetWindowAttributes(_display, _unityWindow, out var attrs) == 0) return;
            if (attrs.width <= 0 || attrs.height <= 0) return;

            IntPtr img = XGetImage(_display, _unityWindow, 0, 0, (uint)attrs.width, (uint)attrs.height, AllPlanes, ZPixmap);
            if (img == IntPtr.Zero) return;

            try
            {
                // XImage.data lives at a fixed offset (after width, height, xoffset,
                // format); read the struct rather than dereferencing byte 0.
                var ximg = Marshal.PtrToStructure<XImage>(img);
                if (ximg.data == IntPtr.Zero) return;
                var bytesPerLine = ximg.bytes_per_line > 0 ? ximg.bytes_per_line : attrs.width * 4;
                var data = new byte[bytesPerLine * attrs.height];
                Marshal.Copy(ximg.data, data, 0, data.Length);
                var rects = GenerateRectangles(data, attrs.width, attrs.height, bytesPerLine);
                XShapeCombineRectangles(_display, _unityWindow, ShapeInput, 0, 0, rects, rects.Length, ShapeSet, YSorted);
                XFlush(_display);
            }
            finally
            {
                XDestroyImage(img);
            }
        }

        private XRectangle[] GenerateRectangles(byte[] data, int width, int height, int bytesPerLine)
        {
            var rects = new List<XRectangle>();
            for (short y = 0; y < height; y++)
            {
                int rowBase = y * bytesPerLine;
                for (short x = 0; x < width; x++)
                {
                    int idx = rowBase + x * 4;
                    if (data[idx + 3] <= ShapeThreshold) continue;
                    short startX = x;
                    while (x < width && data[rowBase + x * 4 + 3] > ShapeThreshold)
                        x++;
                    rects.Add(new XRectangle { X = startX, Y = y, Width = (ushort)(x - startX), Height = 1 });
                }
            }
            return rects.ToArray();
        }

        // ---- Mouse / monitors / windows ----

        public bool GetMousePosition(out Vector2Int position)
        {
            position = Vector2Int.zero;
            if (_display == IntPtr.Zero) return false;
            IntPtr root = IntPtr.Zero, child = IntPtr.Zero;
            int rootX = 0, rootY = 0, winX = 0, winY = 0;
            uint mask = 0;
            if (!XQueryPointer(_display, _rootWindow, ref root, ref child, ref rootX, ref rootY, ref winX, ref winY, ref mask))
                return false;
            position = new Vector2Int(rootX, rootY);
            return true;
        }

        public List<MonitorInfoData> GetAllMonitors()
        {
            var result = new List<MonitorInfoData>();
            if (_display == IntPtr.Zero) return result;
            if (XRRQueryExtension(_display, out _, out _) == 0) return result;

            IntPtr res = XRRGetScreenResourcesCurrent(_display, _rootWindow);
            if (res == IntPtr.Zero) return result;

            try
            {
                var resources = Marshal.PtrToStructure<XrrScreenResources>(res);
                for (int i = 0; i < resources.noutput; i++)
                {
                    IntPtr output = Marshal.ReadIntPtr(resources.outputs, i * IntPtr.Size);
                    IntPtr outInfo = XRRGetOutputInfo(_display, res, output);
                    if (outInfo == IntPtr.Zero) continue;
                    try
                    {
                        var info = Marshal.PtrToStructure<XrrOutputInfo>(outInfo);
                        if ((int)info.connection != 0 || info.crtc == IntPtr.Zero) continue;
                        IntPtr crtc = XRRGetCrtcInfo(_display, res, info.crtc);
                        if (crtc == IntPtr.Zero) continue;
                        try
                        {
                            var ci = Marshal.PtrToStructure<XrrCrtcInfo>(crtc);
                            if (ci.width == 0 || ci.height == 0) continue;
                            result.Add(new MonitorInfoData
                            {
                                Index = i,
                                Name = "Monitor" + i,
                                X = ci.x,
                                Y = ci.y,
                                Width = (int)ci.width,
                                Height = (int)ci.height,
                            });
                        }
                        finally { XRRFreeCrtcInfo(crtc); }
                    }
                    finally { XRRFreeOutputInfo(outInfo); }
                }
            }
            finally { XRRFreeScreenResources(res); }
            return result;
        }

        public List<IntPtr> GetAllVisibleWindows()
        {
            var result = new List<IntPtr>();
            if (_display == IntPtr.Zero) return result;

            var atom = XInternAtom(_display, "_NET_CLIENT_LIST", true);
            if (atom != IntPtr.Zero)
            {
                var status = XGetWindowProperty(_display, _rootWindow, atom, 0, 1024, false, (IntPtr)XaWindow,
                    out var actualType, out var actualFormat, out var nItems, out _, out var prop);
                if (status == 0 && actualType == (IntPtr)XaWindow && actualFormat == 32 && prop != IntPtr.Zero)
                {
                    // _NET_CLIENT_LIST items are format-32 (4-byte XIDs).
                    for (ulong i = 0; i < nItems; i++)
                    {
                        var w = new IntPtr(Marshal.ReadInt32(prop, (int)(i * 4)));
                        if (IsWindowVisible(w)) result.Add(w);
                    }
                    XFree(prop);
                    return result;
                }
                if (prop != IntPtr.Zero) XFree(prop);
                return result;
            }
            return result;
        }

        private bool IsWindowVisible(IntPtr window)
        {
            if (_display == IntPtr.Zero) return false;
            if (XGetWindowAttributes(_display, window, out var attrs) == 0) return false;
            return attrs.map_state == IsViewable;
        }

        private List<IntPtr> FindWindowsByPid(int targetPid)
        {
            var result = new List<IntPtr>();
            var windows = GetAllVisibleWindows();
            foreach (var window in windows)
            {
                if (GetWindowPid(window) == targetPid)
                    result.Add(window);
            }
            return result;
        }

        private int GetWindowPid(IntPtr window)
        {
            if (_display == IntPtr.Zero) return -1;
            var pidAtom = XInternAtom(_display, "_NET_WM_PID", false);
            if (pidAtom == IntPtr.Zero) return -1;
            var status = XGetWindowProperty(_display, window, pidAtom, 0, 1, false, (IntPtr)XaCardinal,
                out _, out _, out var nItems, out _, out var prop);
            if (status == 0 && prop != IntPtr.Zero && nItems > 0)
            {
                var pid = Marshal.ReadInt32(prop);
                XFree(prop);
                return pid;
            }
            if (prop != IntPtr.Zero) XFree(prop);
            return -1;
        }

        public WindowInfoData GetWindowInfo(IntPtr handle)
        {
            var pos = Vector2Int.zero;
            var size = Vector2Int.zero;
            string className = string.Empty;

            if (handle != IntPtr.Zero && _display != IntPtr.Zero)
            {
                if (XGetWindowAttributes(_display, handle, out var attrs) != 0)
                {
                    size = new Vector2Int(attrs.width, attrs.height);
                    XTranslateCoordinates(_display, handle, _rootWindow, 0, 0, out var x, out var y, out _);
                    pos = new Vector2Int(x, y);
                }
                if (XGetClassHint(_display, handle, out var hint) != 0)
                {
                    className = Marshal.PtrToStringAnsi(hint.res_class) ?? string.Empty;
                    XFree(hint.res_name);
                    XFree(hint.res_class);
                }
            }
            return new WindowInfoData(handle, pos, size, className);
        }

        private void ChangeProperty<T>(IntPtr property, IntPtr type, int format, int mode, T data, int nelements)
        {
            var handle = global::System.Runtime.InteropServices.GCHandle.Alloc(data, global::System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                XChangeProperty(_display, _unityWindow, property, type, format, mode, handle.AddrOfPinnedObject(), nelements);
            }
            finally
            {
                handle.Free();
            }
        }

        public void Dispose()
        {
            if (_closing) return;
            _closing = true;
            _shapingRunning = false;
            if (_shapingThread is { IsAlive: true })
                _shapingThread.Join(500);
            if (_display != IntPtr.Zero)
            {
                XSync(_display, false);
                XCloseDisplay(_display);
                _display = IntPtr.Zero;
            }
        }

        // ---- Structs ----

        [StructLayout(LayoutKind.Sequential)]
        private struct XClientMessageEvent
        {
            public int type;
            public IntPtr serial;
            public bool send_event;
            public IntPtr display;
            public IntPtr window;
            public IntPtr message_type;
            public int format;
            public IntPtr data0;
            public IntPtr data1;
            public IntPtr data2;
            public IntPtr data3;
            public IntPtr data4;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XWindowAttributes
        {
            public int x, y;
            public int width, height;
            public int border_width;
            public int depth;
            public IntPtr visual;
            public IntPtr root;
            public int c_class;
            public int bit_gravity;
            public int win_gravity;
            public int backing_store;
            public ulong backing_planes;
            public ulong backing_pixel;
            public bool save_under;
            public IntPtr colormap;
            public bool map_installed;
            public int map_state;
            public long all_event_masks;
            public long your_event_mask;
            public long do_not_propagate_mask;
            public bool override_redirect;
            public IntPtr screen;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XClassHint
        {
            public IntPtr res_name;
            public IntPtr res_class;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XImage
        {
            public int width, height;
            public int xoffset;
            public int format;
            public IntPtr data;
            public int byte_order;
            public int bitmap_unit;
            public int bitmap_bit_order;
            public int bitmap_pad;
            public int depth;
            public int bytes_per_line;
            public int bits_per_pixel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XMotifWmHints
        {
            public IntPtr flags;
            public IntPtr functions;
            public IntPtr decorations;
            public IntPtr input_mode;
            public IntPtr status;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XRectangle
        {
            public short X;
            public short Y;
            public ushort Width;
            public ushort Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrrScreenResources
        {
            public IntPtr timestamp;
            public IntPtr configTimestamp;
            public int ncrtc;
            public IntPtr crtcs;
            public int noutput;
            public IntPtr outputs;
            public int nmode;
            public IntPtr modes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrrOutputInfo
        {
            public IntPtr timestamp;
            public IntPtr crtc;
            public IntPtr name;
            public int nameLen;
            public long mm_width;
            public long mm_height;
            public int connection;
            public byte subpixel_order;
            public int ncrtc;
            public IntPtr crtcs;
            public int nclone;
            public IntPtr clones;
            public int nmode;
            public int npreferred;
            public IntPtr modes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XrrCrtcInfo
        {
            public IntPtr timestamp;
            public int x, y;
            public uint width, height;
            public int mode;
            public int rotation;
            public int noutput;
            public IntPtr outputs;
            public int npossible;
            public IntPtr possible;
        }

        // ---- P/Invoke ----

        [DllImport(LibX11)] private static extern IntPtr XOpenDisplay(string displayName);
        [DllImport(LibX11)] private static extern void XCloseDisplay(IntPtr display);
        [DllImport(LibX11)] private static extern IntPtr XDefaultRootWindow(IntPtr display);
        [DllImport(LibX11)] private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);
        [DllImport(LibX11)] private static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
            long longOffset, long longLength, bool delete, IntPtr reqType, out IntPtr actualTypeReturn,
            out int actualFormatReturn, out ulong nItemsReturn, out ulong bytesAfterReturn, out IntPtr propReturn);
        [DllImport(LibX11)] private static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attrs);
        [DllImport(LibX11)] private static extern int XGetClassHint(IntPtr display, IntPtr w, out XClassHint classHints);
        [DllImport(LibX11)] private static extern int XFree(IntPtr data);
        [DllImport(LibX11)] private static extern int XMoveWindow(IntPtr display, IntPtr window, int x, int y);
        [DllImport(LibX11)] private static extern int XResizeWindow(IntPtr display, IntPtr window, int width, int height);
        [DllImport(LibX11)] private static extern bool XTranslateCoordinates(IntPtr display, IntPtr srcW, IntPtr destW,
            int srcX, int srcY, out int destX, out int destY, out IntPtr child);
        [DllImport(LibX11)] private static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate,
            long eventMask, ref XClientMessageEvent eventSend);
        [DllImport(LibX11)] private static extern int XFlush(IntPtr display);
        [DllImport(LibX11)] private static extern void XSync(IntPtr display, bool discard);
        [DllImport(LibX11)] private static extern bool XQueryPointer(IntPtr display, IntPtr window, ref IntPtr windowReturn,
            ref IntPtr childReturn, ref int rootX, ref int rootY, ref int winX, ref int winY, ref uint mask);
        [DllImport(LibX11)] private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property,
            IntPtr type, int format, int mode, IntPtr data, int nItems);
        [DllImport(LibX11)] private static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y,
            uint width, uint height, ulong planeMask, int format);
        [DllImport(LibX11)] private static extern int XDestroyImage(IntPtr xImage);
        [DllImport(LibXExt)] private static extern void XShapeCombineRectangles(IntPtr display, IntPtr window, int destKind,
            int xOff, int yOff, XRectangle[] rectangles, int nRects, int op, int ordering);
        [DllImport(LibXRandR)] private static extern int XRRQueryExtension(IntPtr display, out int eventBase, out int errorBase);
        [DllImport(LibXRandR)] private static extern IntPtr XRRGetScreenResourcesCurrent(IntPtr display, IntPtr window);
        [DllImport(LibXRandR)] private static extern void XRRFreeScreenResources(IntPtr resources);
        [DllImport(LibXRandR)] private static extern IntPtr XRRGetOutputInfo(IntPtr display, IntPtr resources, IntPtr output);
        [DllImport(LibXRandR)] private static extern void XRRFreeOutputInfo(IntPtr outputInfo);
        [DllImport(LibXRandR)] private static extern IntPtr XRRGetCrtcInfo(IntPtr display, IntPtr resources, IntPtr crtc);
        [DllImport(LibXRandR)] private static extern void XRRFreeCrtcInfo(IntPtr crtcInfo);
    }
}