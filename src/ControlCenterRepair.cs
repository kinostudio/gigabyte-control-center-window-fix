using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class ControlCenterRepair
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr ShellExecuteW(IntPtr owner, string verb, string file, string parameters, string directory, int show);
    [DllImport("user32.dll",SetLastError=true)] static extern bool AllowSetForegroundWindow(int pid);
    static bool AutoLaunch(string[] args) { return args.Length == 0 || (args.Length == 1 && args[0] == "--launch"); }
    static bool Success(string status) { return status.StartsWith("ready;") || status.StartsWith("restarted-as-user"); }
    const string Target = @"C:\Program Files\ControlCenter\ControlCenter.exe";
    static void VerifyTarget(string root) {
        var json=new JavaScriptSerializer();string expected=null;
        foreach(var entry in json.Deserialize<List<Dictionary<string,string>>>(File.ReadAllText(Path.Combine(root,"original-binary-hashes.json"))))
            if(string.Equals(entry["Path"],Target,StringComparison.OrdinalIgnoreCase))expected=entry["Hash"];
        using(var sha=SHA256.Create())using(var file=File.OpenRead(Target))
            if(!string.Equals(expected,BitConverter.ToString(sha.ComputeHash(file)).Replace("-",""),StringComparison.OrdinalIgnoreCase))throw new Exception("未驗證的原廠版本 / Unverified OEM version; nothing started.");
    }
    static int ShowAndCheck(string root) {
        var info=new ProcessStartInfo(Path.Combine(root,"ControlCenterTaskbarFix.exe"),"--show") {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root};
        using(var p=Process.Start(info)) {
            AllowSetForegroundWindow(p.Id);
            if(!p.WaitForExit(10000))throw new Exception("視窗檢查逾時 / Window check timed out; inspect logs before retrying.");
            return p.ExitCode;
        }
    }
    static bool GuiExists() {
        bool found=false;int session=Process.GetCurrentProcess().SessionId;
        foreach(var p in Process.GetProcessesByName("ControlCenter"))using(p){if(p.SessionId==session)found=true;}
        return found;
    }
    static int BackgroundLaunch() {
        bool created;
        using(var mutex=new Mutex(true,@"Local\ControlCenterBackgroundLaunch-"+Process.GetCurrentProcess().SessionId,out created)) {
            if(!created)return 0;
            try {
                string root=AppDomain.CurrentDomain.BaseDirectory,helper=Path.Combine(root,"ControlCenterStartupRepair.exe");
                if(!File.Exists(helper))throw new FileNotFoundException("完整解壓縮後再執行 / Extract the complete bundle first.");
                var elapsed=Stopwatch.StartNew();
                var started=DateTime.UtcNow;
                VerifyTarget(root);
                int quick=ShowAndCheck(root);bool directStart=false;
                if(quick==5&&!GuiExists()) {
                    using(var app=Process.Start(new ProcessStartInfo(Target) {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(Target),WindowStyle=ProcessWindowStyle.Normal})) {
                        directStart=true;
                        // Let the OEM create its window before considering an unnecessary restart.
                        do {Thread.Sleep(500);quick=ShowAndCheck(root);}while(quick==5&&!app.HasExited&&elapsed.Elapsed<TimeSpan.FromSeconds(15));
                    }
                }
                File.WriteAllText(Path.Combine(root,"launch-result.json"),new JavaScriptSerializer().Serialize(new {started=started.ToString("o"),firstCheck=quick,directStart=directStart,elapsedMs=elapsed.ElapsedMilliseconds,repairNeeded=quick!=0}),Encoding.UTF8);
                if(quick==0) {
                    File.WriteAllText(Path.Combine(root,"repair-result.json"),new JavaScriptSerializer().Serialize(new {started=started.ToString("o"),status="ready; repair skipped",action=directStart?"fast-started-without-repair":"fast-reused-without-repair",elapsedMs=elapsed.ElapsedMilliseconds,invariantsChecked=false,note="Read-only window checks only; no registry, service, power or charge writes."}),Encoding.UTF8);
                    return 0;
                }
                if(quick!=2&&quick!=4&&quick!=5)throw new Exception("視窗檢查結果無效 / Invalid inspection result.");
                var info=new ProcessStartInfo(helper) {UseShellExecute=true,Verb="runas",WorkingDirectory=root,WindowStyle=ProcessWindowStyle.Hidden};
                using(var child=Process.Start(info)) {
                    // Pass only this user-initiated launch's foreground right to its helper.
                    bool granted=AllowSetForegroundWindow(child.Id);
                    File.WriteAllText(Path.Combine(root,"foreground-launch.json"),new JavaScriptSerializer().Serialize(new {timestamp=DateTimeOffset.Now.ToString("o"),pid=child.Id,granted=granted}),Encoding.UTF8);
                    if(!child.WaitForExit(180000))throw new Exception("檢查逾時，背景程序可能仍在執行，請查閱日誌勿重複啟動。 / Timed out; inspect logs before retrying.");
                    string path=Path.Combine(root,"repair-result.json");
                    if(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<started)throw new Exception("未取得本次檢查結果 / No current check result.");
                    string json=File.ReadAllText(path,Encoding.UTF8);
                    if(child.ExitCode!=0||!Success(Status(json))) {
                        var report=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
                        object error;throw new Exception(report.TryGetValue("error",out error)?Convert.ToString(error):"請查看 repair-result.json / Check repair-result.json.");
                    }
                }
                return 0;
            }catch(Exception e){MessageBox.Show(e.Message,"Control Center 啟動失敗 / Launch failed",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;}
            finally{mutex.ReleaseMutex();}
        }
    }
    static string Status(string json) {
        var values = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        object value;
        return values.TryGetValue("status", out value) ? Convert.ToString(value) : "unknown";
    }
    [STAThread] static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test") {
            if (!AutoLaunch(new[] { "--launch" }) || !AutoLaunch(new string[0]) || AutoLaunch(new[] { "--launch", "extra" })) return 1;
            if(!Success("ready; repair skipped")||!Success("restarted-as-user; awaiting visual/taskbar acceptance")||Success("failed"))return 1;
            if (Status("{\"status\":\"failed\"}") != "failed" ||
                Status("{\"status\":\"restarted-as-user; awaiting visual/taskbar acceptance\"}").StartsWith("restarted-as-user") == false) return 1;
            return 0;
        }
        bool autoLaunch = AutoLaunch(args);
        if(autoLaunch)return BackgroundLaunch();
        if (args.Length != 1 || args[0] != "--settings") return 2;
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string repair = Path.Combine(root, "ControlCenterStartupRepair.exe");
        string result = Path.Combine(root, "repair-result.json");
        Application.EnableVisualStyles();
        var form = new Form { Text = "Control Center Window Fix / 視窗修復", ClientSize = new Size(680, 350), StartPosition = FormStartPosition.CenterScreen, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog };
        var description = new Label { Left = 20, Top = 20, Width = 640, Height = 140,
            Text = "先檢查視窗、工作列與圖示，正常時略過修復。\r\n只有必要時才重開原廠介面，請先完成其中正在操作的設定。\r\n前後核對服務與充電設定；不修改原廠程式檔。\r\n\r\nChecks first, reuses a healthy window and repairs only when needed.\r\nCharge, power plan and service are checked before/after." };
        var button = new Button { Text = "修復並開啟 / Repair and open Control Center", Left = 20, Top = 170, Width = 440, Height = 35 };
        var shortcut = new Button { Text = "建立桌面捷徑 / Create desktop shortcut", Left = 20, Top = 210, Width = 440, Height = 35 };
        shortcut.Click += delegate {
            try {
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic link = shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Control Center - Auto Repair.lnk"));
                try {
                    link.TargetPath = Application.ExecutablePath; link.Arguments = "--launch"; link.WorkingDirectory = root;
                    string icon = @"C:\Program Files\ControlCenter\icon.ico";
                    if (File.Exists(icon)) link.IconLocation = icon + ",0";
                    link.Save();
                } finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
                MessageBox.Show(form, "桌面捷徑已建立 / Desktop shortcut created.");
            } catch (Exception e) { MessageBox.Show(form, e.Message); }
        };
        var state = new Label { Left = 20, Top = 260, Width = 640, Height = 75, Text = "尚未執行 / Ready. 按下按鈕後請手動完成 UAC 授權。Approve UAC manually." };
        var timer = new System.Windows.Forms.Timer { Interval = 500 };
        DateTime started = DateTime.MaxValue;
        timer.Tick += delegate {
            try {
                if (DateTime.UtcNow - started > TimeSpan.FromMinutes(3)) {
                    timer.Stop(); button.Enabled = true;
                    state.Text = "等候逾時／Timed out. 請檢查 repair-result.json 與 taskbar-diagnostic.txt；勿重複執行。";
                    return;
                }
                if (!File.Exists(result) || File.GetLastWriteTimeUtc(result) < started) return;
                string status = Status(File.ReadAllText(result, Encoding.UTF8));
                timer.Stop(); button.Enabled = true;
                state.Text = Success(status) ?
                    "完成前後核對／Checks passed. 請確認視窗、圖示與縮圖。\r\nPlease verify the window, taskbar icon and thumbnail visually." :
                    "修復未完成 / Failed. See repair-result.json and taskbar-diagnostic.txt.";
            } catch (IOException) { } catch (ArgumentException) { }
        };
        button.Click += delegate {
            if (!File.Exists(repair)) { state.Text = "缺少修復工具 / Missing repair helper. No changes started."; return; }
            started = DateTime.UtcNow;
            button.Enabled = false;
            state.Text = "等待授權 / Awaiting UAC. 授權後會關閉並重開原廠介面。";
            // A visible owner provides an interactive entry point; UAC is never bypassed.
            long code = ShellExecuteW(form.Handle, "runas", repair, null, root, 0).ToInt64();
            if (code <= 32) {
                state.Text = "未授權或啟動失敗 / Not authorized or launch failed (code " + code + "). No repair started.";
                button.Enabled = true;
                return;
            }
            state.Text = "正在修復 / Repairing. Waiting for before/after verification.";
            timer.Start();
        };
        form.Controls.Add(description); form.Controls.Add(button); form.Controls.Add(shortcut); form.Controls.Add(state);
        form.FormClosed += delegate { timer.Dispose(); };
        Application.Run(form);
        return 0;
    }
}
