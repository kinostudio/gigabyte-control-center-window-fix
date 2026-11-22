using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class ControlCenterWindowFix
{
    const string Target = @"C:\Program Files\ControlCenter\ControlCenter.exe";
    const long ToolWindow = 0x80, AppWindow = 0x40000;
    const int ExStyle = -20;
    static readonly string Report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "taskbar-diagnostic.txt");
    static void Log(string message) {
        try { File.AppendAllText(Report, DateTimeOffset.Now.ToString("o") + " " + message + Environment.NewLine); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    delegate bool WindowCallback(IntPtr window, IntPtr state);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr state);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int length);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("kernel32.dll")] static extern void SetLastError(uint code);
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref int size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr kind, IntPtr value, uint flags, uint timeout, out IntPtr result);
    static IntPtr IconMessage(IntPtr window, uint message, int kind, IntPtr value) {
        IntPtr result;
        if (SendMessageTimeout(window, message, new IntPtr(kind), value, 2, 2000, out result) == IntPtr.Zero)
            throw new Exception("原廠視窗未回應圖示設定／讀回，未確認圖示修復成功。");
        return result;
    }

    static long TaskbarStyle(long original) { return (original & ~ToolWindow) | AppWindow; }
    static bool StyleCallFailed(IntPtr previous, int error) { return previous == IntPtr.Zero && error != 0; }
    static bool IsTarget(string path) { return string.Equals(path, Target, StringComparison.OrdinalIgnoreCase); }
    static string ProcessPath(int pid) {
        IntPtr h=OpenProcess(0x1000,false,pid);if(h==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        try {var path=new StringBuilder(1024);int size=path.Capacity;if(!QueryFullProcessImageName(h,0,path,ref size))throw new Win32Exception(Marshal.GetLastWin32Error());return path.ToString();}
        finally {CloseHandle(h);}
    }
    static bool Activate(IntPtr window) {
        if(IsIconic(window)||!IsWindowVisible(window))ShowWindow(window,IsZoomed(window)?3:9);
        // HWND_TOP raises once in the normal Z-order; it never sets HWND_TOPMOST.
        bool raised=SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x0001|0x0002|0x0010);
        int raiseError=raised?0:Marshal.GetLastWin32Error();
        bool requested=SetForegroundWindow(window),foreground=GetForegroundWindow()==window;
        uint pid;GetWindowThreadProcessId(window,out pid);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"activation-result.json"),new JavaScriptSerializer().Serialize(new {timestamp=DateTimeOffset.Now.ToString("o"),pid=pid,raised=raised,raiseError=raiseError,requested=requested,isForeground=foreground}));
        Log("Activation: Raised="+raised+" ForegroundRequest="+requested+" IsForeground="+foreground);
        return requested;
    }
    static long Score(int width, int height, bool visible) {
        return !visible || width < 300 || height < 200 ? -1 : (long)width * height;
    }
    static long CandidateScore(int width,int height,bool visible,bool minimized) {
        long score=minimized?60000:Score(width,height,true);
        return score<0?-1:score+(visible?(1L<<40):0);
    }
    static IntPtr WaitForWindow(Func<IntPtr> probe, Action pause, int attempts) {
        for (int i = 0; i < attempts; i++) {
            IntPtr window = probe();
            if (window != IntPtr.Zero) return window;
            if (i + 1 < attempts) pause();
        }
        return IntPtr.Zero;
    }
    static void Check(bool condition) { if (!condition) throw new Exception("Self-check failed"); }
    static bool MatchingIcon(IntPtr handle, Icon expected) {
        if (handle == IntPtr.Zero) return false;
        try {
            using (var actual = Icon.FromHandle(handle).ToBitmap()) using (var wanted = expected.ToBitmap()) {
                if (actual.Size != wanted.Size) return false;
                for (int y=0;y<actual.Height;y++) for(int x=0;x<actual.Width;x++)
                    if(actual.GetPixel(x,y).ToArgb()!=wanted.GetPixel(x,y).ToArgb()) return false;
                return true;
            }
        } catch (ArgumentException) { return false; }
    }
    // 0: healthy; 4: visible current-user window needs style/icon repair; 5: no usable window.
    static int Inspect(bool activate,bool showFirst=false) {
        int session=Process.GetCurrentProcess().SessionId;
        var targets=new HashSet<int>();
        foreach(var p in Process.GetProcessesByName("ControlCenter")) using(p) {
            if(p.SessionId!=session)continue;
            if(!IsTarget(ProcessPath(p.Id)))throw new Exception("Unexpected GUI path.");
            using(var process=new ManagementObject("Win32_Process.Handle='"+p.Id+"'"))
            using(var owner=process.InvokeMethod("GetOwner",null,null)) {
                if(Convert.ToUInt32(owner["ReturnValue"])!=0)throw new Exception("Cannot verify owner.");
                string account=Convert.ToString(owner["Domain"])+"\\"+Convert.ToString(owner["User"]);
                if(!string.Equals(account,WindowsIdentity.GetCurrent().Name,StringComparison.OrdinalIgnoreCase))return 5;
            }
            targets.Add(p.Id);
        }
        IntPtr chosen=IntPtr.Zero;long best=-1;
        EnumWindows(delegate(IntPtr w,IntPtr unused) {
            uint pid;GetWindowThreadProcessId(w,out pid);if(!targets.Contains((int)pid))return true;
            var name=new StringBuilder(256);GetClassName(w,name,name.Capacity);Rect rect;
            if(!name.ToString().StartsWith("HwndWrapper[",StringComparison.Ordinal)||(!showFirst&&!IsWindowVisible(w))||!GetWindowRect(w,out rect))return true;
            long score=CandidateScore(rect.Right-rect.Left,rect.Bottom-rect.Top,IsWindowVisible(w),IsIconic(w));
            if(score>best){best=score;chosen=w;}return true;
        },IntPtr.Zero);
        if(chosen==IntPtr.Zero)return 5;
        long style=GetWindowLongPtr(chosen,ExStyle).ToInt64();
        Log("Inspection: show="+showFirst+" HWND="+chosen+" visible="+IsWindowVisible(chosen)+" style=0x"+style.ToString("X"));
        int state=((style&AppWindow)==0||(style&ToolWindow)!=0)?4:0;
        string icon=Path.Combine(Path.GetDirectoryName(Target),"icon.ico");
        using(var large=new Icon(icon,GetSystemMetrics(11),GetSystemMetrics(12)))
        using(var small=new Icon(icon,GetSystemMetrics(49),GetSystemMetrics(50))) {
            if(!MatchingIcon(IconMessage(chosen,0x7F,1,IntPtr.Zero),large)||!MatchingIcon(IconMessage(chosen,0x7F,0,IntPtr.Zero),small))state=4;
        }
        // Do not classify the transient WPF notifications caused by showing as pre-existing damage.
        if(showFirst||(activate&&state==0))Activate(chosen);
        return state;
    }
    [STAThread] static int Main(string[] args)
    {
        // This branch is pure: it never queries or changes a Windows application.
        if (args.Length == 1 && args[0] == "--self-test") {
            Check(CandidateScore(800,600,true,false)>CandidateScore(4000,3000,false,false));
            Check(CandidateScore(160,28,true,true)>CandidateScore(4000,3000,false,false));
            Check(CandidateScore(10,10,false,false)==-1);
            Check(!MatchingIcon(IntPtr.Zero,SystemIcons.Application));
            Check(MatchingIcon(SystemIcons.Application.Handle,SystemIcons.Application));
            Check(TaskbarStyle(ToolWindow | 0x80000) == (AppWindow | 0x80000));
            Check(TaskbarStyle(AppWindow) == AppWindow);
            Check(IsTarget(Target.ToUpperInvariant()));
            Check(!IsTarget(@"C:\Other\ControlCenter.exe"));
            Check(Score(200, 100, true) == -1);
            Check(Score(800, 600, true) > Score(1600, 900, false));
            Check(Score(1600, 900, false) == -1);
            int probes = 0, pauses = 0;
            Check(WaitForWindow(delegate { return ++probes == 3 ? new IntPtr(7) : IntPtr.Zero; }, delegate { pauses++; }, 4) == new IntPtr(7));
            Check(probes == 3 && pauses == 2);
            probes = 0; pauses = 0;
            Check(WaitForWindow(delegate { probes++; return IntPtr.Zero; }, delegate { pauses++; }, 3) == IntPtr.Zero);
            Check(probes == 3 && pauses == 2);
            Check(StyleCallFailed(IntPtr.Zero, 5));
            Check(!StyleCallFailed(IntPtr.Zero, 0));
            Check(!StyleCallFailed(new IntPtr(0x80000), 5));
            return 0;
        }
        if (args.Length == 1 && (args[0] == "--check" || args[0] == "--activate" || args[0] == "--show")) {
            try { return Inspect(args[0] == "--activate",args[0]=="--show"); } catch(Exception e) { Log("Inspection failed: "+e.Message); return 2; }
        }
        if (args.Length != 0) return 2;
        Log("Taskbar stage started. Executable=" + Application.ExecutablePath);
        try {
            using (var identity = WindowsIdentity.GetCurrent()) {
                if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) {
                    throw new Exception("請從可見的修復入口執行；此階段不會自行提升權限。");
                }
            }
            int session = Process.GetCurrentProcess().SessionId;
            Log("Elevated instance; session=" + session);
            IntPtr chosen = IntPtr.Zero;
            int chosenPid = 0;
            var targets = new HashSet<int>();
            foreach (var process in Process.GetProcessesByName("ControlCenter")) {
                using (process) {
                    if (process.SessionId == session && IsTarget(process.MainModule.FileName)) targets.Add(process.Id);
                }
            }
            if (targets.Count == 0) throw new Exception("原廠 Control Center 未在目前工作階段執行。");
            var readiness = Stopwatch.StartNew();
            int scan = 0;
            chosen = WaitForWindow(delegate {
            scan++;
            long best = -1;
            IntPtr ready = IntPtr.Zero;
            EnumWindows(delegate(IntPtr window, IntPtr unused) {
                uint pid;
                GetWindowThreadProcessId(window, out pid);
                if (!targets.Contains((int)pid)) return true;
                try {
                    var name = new StringBuilder(256);
                    GetClassName(window, name, name.Capacity);
                    Rect rect;
                    if (!GetWindowRect(window, out rect)) return true;
                    if (scan == 1 || IsWindowVisible(window)) Log("Candidate: PID=" + pid + " HWND=" + window + " class=" + name +
                        " size=" + (rect.Right - rect.Left) + "x" + (rect.Bottom - rect.Top) +
                        " visible=" + IsWindowVisible(window) + " exstyle=0x" + GetWindowLongPtr(window, ExStyle).ToInt64().ToString("X"));
                    if (!name.ToString().StartsWith("HwndWrapper[", StringComparison.Ordinal)) return true;
                    long score = IsIconic(window) ? 60000 : Score(rect.Right - rect.Left, rect.Bottom - rect.Top, IsWindowVisible(window));
                    if (score > best) { best = score; ready = window; chosenPid = (int)pid; }
                } catch (ArgumentException) { } catch (InvalidOperationException) { } catch (Win32Exception windowError) { Log("Window query denied/failed: " + windowError.NativeErrorCode); }
                return true;
            }, IntPtr.Zero);
            return ready;
            }, delegate { Thread.Sleep(500); }, 90);
            if (chosen == IntPtr.Zero) throw new Exception("等候 45 秒後仍無可見的原廠主視窗；尚未變更任何視窗樣式。請先讓原廠介面完成啟動。");
            Log("Visible main window ready after " + readiness.ElapsedMilliseconds + " ms; scan=" + scan);
            using (var verified = Process.GetProcessById(chosenPid)) {
                if (verified.SessionId != session || !IsTarget(verified.MainModule.FileName)) throw new Exception("目標程序已改變，未修改視窗。");
            }
            uint currentPid;
            GetWindowThreadProcessId(chosen, out currentPid);
            if (currentPid != chosenPid) throw new Exception("目標視窗已改變，未修改視窗。");
            using (var process = new ManagementObject("Win32_Process.Handle='" + chosenPid + "'"))
            using (var owner = process.InvokeMethod("GetOwner", (ManagementBaseObject)null, null)) {
                string account = Convert.ToString(owner["Domain"]) + "\\" + Convert.ToString(owner["User"]);
                if (Convert.ToUInt32(owner["ReturnValue"]) != 0 || !string.Equals(account, WindowsIdentity.GetCurrent().Name, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("拒絕修改其他帳戶或 SYSTEM 的視窗；請先完成原廠介面啟動身分修復。");
            }
            long original = GetWindowLongPtr(chosen, ExStyle).ToInt64();
            string iconPath = @"C:\Program Files\ControlCenter\icon.ico";
            if (!File.Exists(iconPath)) throw new FileNotFoundException("缺少原廠圖示，未套用替代圖案。", iconPath);
            using (var largeIcon = new Icon(iconPath, GetSystemMetrics(11), GetSystemMetrics(12)))
            using (var smallIcon = new Icon(iconPath, GetSystemMetrics(49), GetSystemMetrics(50))) {
            IntPtr previousLarge = IconMessage(chosen, 0x7F, 1, IntPtr.Zero);
            IntPtr previousSmall = IconMessage(chosen, 0x7F, 0, IntPtr.Zero);
            try {
            IconMessage(chosen, 0x80, 1, largeIcon.Handle);
            IconMessage(chosen, 0x80, 0, smallIcon.Handle);
            if (IconMessage(chosen, 0x7F, 1, IntPtr.Zero) != largeIcon.Handle ||
                IconMessage(chosen, 0x7F, 0, IntPtr.Zero) != smallIcon.Handle)
                throw new Exception("原廠視窗沒有保留指定圖示。");
            Log("OEM icon assigned and read back: " + iconPath);
            long updated = TaskbarStyle(original);
            Log("Selected HWND=" + chosen + " original=0x" + original.ToString("X"));
            bool minimized = IsIconic(chosen), maximized = IsZoomed(chosen);
            // The Shell must see a hide/style-change/show cycle to rebuild its taskbar button.
            ShowWindow(chosen, 0);
            try {
            SetLastError(0);
            IntPtr previous = SetWindowLongPtr(chosen, ExStyle, new IntPtr(updated));
            int error = Marshal.GetLastWin32Error();
            Log("Style return=0x" + previous.ToInt64().ToString("X") + " lastError=" + error +
                " readback=0x" + GetWindowLongPtr(chosen, ExStyle).ToInt64().ToString("X"));
            if (StyleCallFailed(previous, error)) throw new Win32Exception(error);
            if (!SetWindowPos(chosen, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0020)) {
                int moveError = Marshal.GetLastWin32Error();
                SetWindowLongPtr(chosen, ExStyle, new IntPtr(original));
                throw new Win32Exception(moveError);
            }
            } catch {
                SetWindowLongPtr(chosen, ExStyle, new IntPtr(original));
                throw;
            } finally { ShowWindow(chosen, maximized ? 3 : minimized ? 9 : 5); }
            bool activated = Activate(chosen);
            Log("ForegroundRequest=" + activated + " IsForeground=" + (GetForegroundWindow() == chosen));
            long finalStyle = GetWindowLongPtr(chosen, ExStyle).ToInt64();
            if ((finalStyle & AppWindow) == 0 || (finalStyle & ToolWindow) != 0) throw new Exception("工作列視窗樣式未能保留。");
            if (IconMessage(chosen, 0x7F, 1, IntPtr.Zero) != largeIcon.Handle ||
                IconMessage(chosen, 0x7F, 0, IntPtr.Zero) != smallIcon.Handle)
                throw new Exception("重新顯示視窗後，原廠圖示未能保留。");
            string resultFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "taskbar-window-result.txt");
            File.WriteAllText(resultFile + ".tmp",
                DateTimeOffset.Now.ToString("o") + Environment.NewLine + "PID=" + chosenPid +
                " HWND=" + chosen + " OriginalExStyle=0x" + original.ToString("X") +
                " UpdatedExStyle=0x" + GetWindowLongPtr(chosen, ExStyle).ToInt64().ToString("X") +
                " ForegroundRequest=" + activated + " IsForeground=" + (GetForegroundWindow() == chosen) +
                " IconReadback=True IconSource=" + iconPath);
            File.Move(resultFile + ".tmp", resultFile);
            } catch {
                try { IconMessage(chosen, 0x80, 1, previousLarge); IconMessage(chosen, 0x80, 0, previousSmall); }
                catch (Exception restoreError) { Log("Icon rollback: " + restoreError.Message); }
                throw;
            }
            // WM_SETICON retains handles, so keep them alive until the OEM process exits.
            using (var targetLifetime = Process.GetProcessById(chosenPid)) targetLifetime.WaitForExit();
            }
            return 0;
        } catch (Exception error) {
            Log("FAILED: " + error);
            return 1;
        }
    }
}
