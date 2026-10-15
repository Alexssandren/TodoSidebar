using System.Runtime.InteropServices;

namespace TodoSidebar.Janela;

static class Win32Janela
{
    public const int WmHotkey = 0x0312;
    public const int WmGetMinMaxInfo = 0x0024;
    public const int WmDisplayChange = 0x007E;
    public const int WmSettingChange = 0x001A;
    public const int WmDpiChanged = 0x02E0;
    public const int WmWindowPosChanging = 0x0046;
    public const int SpiSetWorkArea = 0x002F;
    public const int IdAtalho = 1;

    const int GwlExStyle = -20;
    const int WsExNoActivate = 0x08000000;
    const int WsExToolWindow = 0x00000080;
    const int WsExAppWindow = 0x00040000;
    const uint SwpNoSize = 0x0001;
    const uint SwpNoMove = 0x0002;
    const uint SwpNoZOrder = 0x0004;
    const uint SwpNoActivate = 0x0010;
    const uint SwpShowWindow = 0x0040;
    const uint SwpFrameChanged = 0x0020;
    const uint ModAlt = 0x0001;
    const uint ModControl = 0x0002;
    const uint ModNoRepeat = 0x4000;
    const uint VkSpace = 0x20;
    const int MonitorDefaultToPrimary = 1;
    const int DwmwaWindowCornerPreference = 33;
    const int DwmwcpDoNotRound = 1;

    public static void Preparar(IntPtr hwnd)
    {
        DefinirSemAtivar(hwnd, true);
        int cantos = DwmwcpDoNotRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref cantos, sizeof(int));
    }

    public static void DefinirSemAtivar(IntPtr hwnd, bool semAtivar)
    {
        var estilo = GetExStyle(hwnd);
        if (semAtivar)
            estilo |= WsExNoActivate;
        else
            estilo &= ~WsExNoActivate;
        estilo |= WsExToolWindow;
        estilo &= ~WsExAppWindow;
        SetExStyle(hwnd, estilo);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    public static bool RegistrarAtalho(IntPtr hwnd) =>
        RegisterHotKey(hwnd, IdAtalho, ModControl | ModAlt | ModNoRepeat, VkSpace);

    public static void LiberarAtalho(IntPtr hwnd) =>
        UnregisterHotKey(hwnd, IdAtalho);

    public static void MoverTopo(IntPtr hwnd, Retangulo retangulo)
    {
        SetWindowPos(hwnd, new IntPtr(-1), retangulo.X, retangulo.Y, retangulo.Largura, retangulo.Altura,
            SwpNoActivate | SwpShowWindow);
    }

    public static void ForcarRetangulo(IntPtr lParam, Retangulo retangulo)
    {
        var pos = Marshal.PtrToStructure<WindowPos>(lParam);
        pos.x = retangulo.X;
        pos.y = retangulo.Y;
        pos.cx = retangulo.Largura;
        pos.cy = retangulo.Altura;
        pos.flags &= ~3u;
        Marshal.StructureToPtr(pos, lParam, false);
    }

    public static bool TamanhoMinimoZero(int msg, IntPtr lParam)
    {
        if (msg != WmGetMinMaxInfo)
            return false;

        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        info.ptMinTrackSize = new Ponto { X = 1, Y = 1 };
        Marshal.StructureToPtr(info, lParam, false);
        return true;
    }

    public static Retangulo AreaUtil()
    {
        var monitor = MonitorFromPoint(new Ponto { X = 0, Y = 0 }, MonitorDefaultToPrimary);
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
            throw new InvalidOperationException("GetMonitorInfo falhou.");

        return new Retangulo(
            info.rcWork.Left,
            info.rcWork.Top,
            info.rcWork.Right - info.rcWork.Left,
            info.rcWork.Bottom - info.rcWork.Top);
    }

    public static uint Dpi()
    {
        var monitor = MonitorFromPoint(new Ponto { X = 0, Y = 0 }, MonitorDefaultToPrimary);
        if (GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 && dpi != 0)
            return dpi;
        return 96;
    }

    public static Retangulo RetanguloAtual(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out RectWin32 retangulo);
        return new Retangulo(
            retangulo.Left,
            retangulo.Top,
            retangulo.Right - retangulo.Left,
            retangulo.Bottom - retangulo.Top);
    }

    public static bool CursorDentro(IntPtr hwnd)
    {
        if (!GetCursorPos(out Ponto ponto) || !GetWindowRect(hwnd, out RectWin32 retangulo))
            return false;

        return ponto.X >= retangulo.Left
            && ponto.X < retangulo.Right
            && ponto.Y >= retangulo.Top
            && ponto.Y < retangulo.Bottom;
    }

    public static IntPtr JanelaDaFrente() => GetForegroundWindow();

    public static bool JanelaExiste(IntPtr hwnd) => hwnd != IntPtr.Zero && IsWindow(hwnd);

    public static void Ativar(IntPtr hwnd) => SetForegroundWindow(hwnd);

    public static void TrazerParaDesktopDaJanelaFrente(IntPtr hwnd)
    {
        IntPtr frente = GetForegroundWindow();
        if (frente == IntPtr.Zero || frente == hwnd)
            return;

        try
        {
            var gerenciador = (IVirtualDesktopManager)new CVirtualDesktopManager();
            if (gerenciador.GetWindowDesktopId(frente, out Guid id) != 0 || id == Guid.Empty)
                return;
            gerenciador.MoveWindowToDesktop(hwnd, ref id);
        }
        catch (COMException)
        {
        }
        catch (InvalidCastException)
        {
        }
    }

    static IntPtr GetExStyle(IntPtr hwnd) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, GwlExStyle) : new IntPtr(GetWindowLong32(hwnd, GwlExStyle));

    static void SetExStyle(IntPtr hwnd, IntPtr estilo)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hwnd, GwlExStyle, estilo);
        else
            SetWindowLong32(hwnd, GwlExStyle, estilo.ToInt32());
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RectWin32 lpRect);

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out Ponto lpPoint);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromPoint(Ponto pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("Shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    struct Ponto
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RectWin32
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MonitorInfo
    {
        public int cbSize;
        public RectWin32 rcMonitor;
        public RectWin32 rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WindowPos
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MinMaxInfo
    {
        public Ponto ptReserved;
        public Ponto ptMaxSize;
        public Ponto ptMaxPosition;
        public Ponto ptMinTrackSize;
        public Ponto ptMaxTrackSize;
    }

    [ComImport]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    [ComImport]
    [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
    class CVirtualDesktopManager
    {
    }
}
