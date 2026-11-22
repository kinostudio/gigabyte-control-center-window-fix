using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class ControlCenterRepair
{
    static readonly string Shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr ShellExecuteW(IntPtr owner, string verb, string file, string parameters, string directory, int show);
    static string Command(string script) { return "-NoProfile -File \"" + script + "\""; }
    static string Status(string json) {
        var values = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        object value;
        return values.TryGetValue("status", out value) ? Convert.ToString(value) : "unknown";
    }
    [STAThread] static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test") {
            if (Status("{\"status\":\"failed\"}") != "failed" ||
                Status("{\"status\":\"restarted-as-user; awaiting visual/taskbar acceptance\"}").StartsWith("restarted-as-user") == false ||
                Command(@"C:\folder with spaces\repair.ps1") != "-NoProfile -File \"C:\\folder with spaces\\repair.ps1\"") return 1;
            return 0;
        }
        if (args.Length != 0) return 2;
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string script = Path.Combine(root, "repair-ui-startup.ps1");
        string result = Path.Combine(root, "repair-result.json");
        Application.EnableVisualStyles();
        var form = new Form { Text = "Control Center Window Fix / 視窗修復", ClientSize = new Size(680, 310), StartPosition = FormStartPosition.CenterScreen, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog };
        var description = new Label { Left = 20, Top = 20, Width = 640, Height = 140,
            Text = "備份並重設原廠 FirstTime 介面旗標，再以登入使用者重新開啟原廠主程式。\r\n\r\n會關閉目前的 Control Center 介面，請先完成介面中的操作。\r\n前後核對服務與充電設定；不修改原廠程式檔。\r\n\r\nRestarts the verified OEM GUI and repairs its window/taskbar.\r\nBacks up FirstTime; checks charge, power plan and service before/after." };
        var button = new Button { Text = "修復並開啟 / Repair and open Control Center", Left = 20, Top = 170, Width = 440, Height = 35 };
        var state = new Label { Left = 20, Top = 220, Width = 640, Height = 75, Text = "尚未執行 / Ready. 按下按鈕後請手動完成 UAC 授權。Approve UAC manually." };
        var timer = new Timer { Interval = 500 };
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
                state.Text = status.StartsWith("restarted-as-user") ?
                    "完成前後核對／Checks passed. 請確認視窗、圖示與縮圖。\r\nPlease verify the window, taskbar icon and thumbnail visually." :
                    "修復未完成 / Failed. See repair-result.json and taskbar-diagnostic.txt.";
            } catch (IOException) { } catch (ArgumentException) { }
        };
        button.Click += delegate {
            if (!File.Exists(script) || !File.Exists(Shell)) { state.Text = "缺少腳本或 PowerShell / Missing script or PowerShell. No changes started."; return; }
            started = DateTime.UtcNow;
            button.Enabled = false;
            state.Text = "等待授權 / Awaiting UAC. 授權後會關閉並重開原廠介面。";
            // A visible owner provides an interactive entry point; UAC is never bypassed.
            long code = ShellExecuteW(form.Handle, "runas", Shell, Command(script), root, 0).ToInt64();
            if (code <= 32) {
                state.Text = "未授權或啟動失敗 / Not authorized or launch failed (code " + code + "). No repair started.";
                button.Enabled = true;
                return;
            }
            state.Text = "正在修復 / Repairing. Waiting for before/after verification.";
            timer.Start();
        };
        form.Controls.Add(description); form.Controls.Add(button); form.Controls.Add(state);
        form.FormClosed += delegate { timer.Dispose(); };
        Application.Run(form);
        return 0;
    }
}
