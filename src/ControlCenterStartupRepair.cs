using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

static class ControlCenterStartupRepair {
    const string Target = @"C:\Program Files\ControlCenter\ControlCenter.exe";
    const string Key = @"SOFTWARE\GIGABYTE\SmartManager";
    static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static string Hash(string file) { using(var sha=SHA256.Create()) using(var s=File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", ""); }
    static bool SameCharge(int[] a,int[] b) { return a[0]==b[0] && a[1]==b[1]; }
    static int[] Charge() {
        using(var search=new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM GB_WMIACPI_Get")) {
            search.Options.Timeout=TimeSpan.FromSeconds(5);
            using(var rows=search.Get()) foreach(ManagementObject row in rows) using(row) {
                var options=new InvokeMethodOptions { Timeout=TimeSpan.FromSeconds(5) };
                using(var p=row.InvokeMethod("GetChargePolicy",null,options))
                using(var s=row.InvokeMethod("GetChargeStop",null,options)) {
                    int policy=Convert.ToInt32(p["Data"]), limit=Convert.ToInt32(s["Data"]);
                    if(limit<60 || limit>100) throw new Exception("Invalid OEM charge limit.");
                    return new[]{policy,limit};
                }
            }
        }
        throw new Exception("Original charge getters unavailable; repair stopped.");
    }
    static string Power() {
        var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"powercfg.exe"),"/getactivescheme") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true };
        using(var p=Process.Start(info)) { string result=p.StandardOutput.ReadToEnd().Trim(); if(!p.WaitForExit(10000)||p.ExitCode!=0)throw new Exception("Power scheme query failed.");return result; }
    }
    static string Service() {
        using(var s=new ManagementObject("Win32_Service.Name='SMV4_Service'")) { s.Get();return Convert.ToString(s["State"]); }
    }
    static string Owner(int pid) {
        using(var p=new ManagementObject("Win32_Process.Handle='"+pid+"'"))
        using(var owner=p.InvokeMethod("GetOwner",null,new InvokeMethodOptions {Timeout=TimeSpan.FromSeconds(5)})) {
            if(Convert.ToUInt32(owner["ReturnValue"])!=0)throw new Exception("Cannot verify GUI owner.");
            return Convert.ToString(owner["Domain"])+"\\"+Convert.ToString(owner["User"]);
        }
    }
    static ProcessStartInfo Start(string exe) { return new ProcessStartInfo(exe) {UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe),CreateNoWindow=true}; }
    static int Main(string[] args) {
        if(args.Length==1 && args[0]=="--self-test") {
            if(!SameCharge(new[]{4,60},new[]{4,60}) || SameCharge(new[]{4,60},new[]{4,80}) || SameCharge(new[]{4,60},new[]{0,60}))return 1;
            return Json.Deserialize<int[]>(Json.Serialize(new[]{4,60}))[1]==60 ? 0 : 1;
        }
        if(args.Length!=0)return 2;
        var report=new Dictionary<string,object> {{"started",DateTimeOffset.Now.ToString("o")},{"status","starting"}};
        int[] before=null;string power=null,service=null;bool changed=false,existed=false;object value=null;RegistryValueKind kind=RegistryValueKind.DWord;Process gui=null;
        using(var mutex=new Mutex(false,@"Local\ControlCenterStartupRepair-"+Process.GetCurrentProcess().SessionId)) {
        if(!mutex.WaitOne(0))return 3;
        try {
            if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new Exception("Administrator permission required.");
            var manifest=Json.Deserialize<List<Dictionary<string,string>>>(File.ReadAllText(Path.Combine(Root,"original-binary-hashes.json")));
            string expected=null;foreach(var entry in manifest)if(string.Equals(entry["Path"],Target,StringComparison.OrdinalIgnoreCase))expected=entry["Hash"];
            string actual=Hash(Target);if(!string.Equals(expected,actual,StringComparison.OrdinalIgnoreCase))throw new Exception("Unverified OEM executable; nothing changed.");
            report["originalBinaryHash"]=actual;
            before=Charge();power=Power();service=Service();
            report["chargeBefore"]=new{policy=before[0],limit=before[1]};report["powerBefore"]=power;report["serviceBefore"]=service;
            string helper=Path.Combine(Root,"ControlCenterTaskbarFix.exe");if(!File.Exists(helper))throw new Exception("Missing taskbar helper; nothing changed.");
            using(var key=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64).OpenSubKey(Key,true)) {
                if(key==null)throw new Exception("OEM registry key unavailable.");
                existed=Array.Exists(key.GetValueNames(),n=>n=="FirstTime");
                if(existed){value=key.GetValue("FirstTime");kind=key.GetValueKind("FirstTime");}
                File.WriteAllText(Path.Combine(Root,"FirstTime-backup.json"),Json.Serialize(new{exists=existed,value=value,kind=existed?kind.ToString():null}),new UTF8Encoding(false));
                int session=Process.GetCurrentProcess().SessionId;
                var targets=new List<Process>();
                try {
                    foreach(var p in Process.GetProcessesByName("ControlCenter")) {
                        if(p.SessionId!=session){p.Dispose();continue;}
                        targets.Add(p);
                        if(!string.Equals(p.MainModule.FileName,Target,StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected GUI path; nothing stopped.");
                    }
                    foreach(var p in targets){p.Kill();if(!p.WaitForExit(10000))throw new Exception("Original GUI did not stop.");}
                } finally {foreach(var p in targets)p.Dispose();}
                if(existed){key.DeleteValue("FirstTime");changed=true;}
            }
            gui=Process.Start(Start(Target));report["newProcessId"]=gui.Id;
            try { gui.WaitForInputIdle(15000); }catch(InvalidOperationException){}
            Thread.Sleep(3000);
            bool current=string.Equals(Owner(gui.Id),WindowsIdentity.GetCurrent().Name,StringComparison.OrdinalIgnoreCase);
            report["newOwnerIsCurrentUser"]=current;if(!current)throw new Exception("OEM GUI is not owned by the current account.");
            string result=Path.Combine(Root,"taskbar-window-result.txt");if(File.Exists(result))File.Delete(result);
            using(var job=Process.Start(Start(helper))) {
                var deadline=DateTime.UtcNow.AddSeconds(60);
                while(!File.Exists(result)) {
                    if(job.HasExited)throw new Exception("Taskbar stage exited; inspect taskbar-diagnostic.txt.");
                    if(DateTime.UtcNow>deadline)throw new Exception("Taskbar stage timed out; inspect diagnostics before retrying.");
                    Thread.Sleep(250);
                }
                string text=File.ReadAllText(result);report["windowResult"]=text;
                if(!text.Contains("IconReadback=True"))throw new Exception("OEM icon readback not confirmed.");
                report["iconKeeperProcessId"]=job.Id;
            }
            report["status"]="restarted-as-user; awaiting visual/taskbar acceptance";
        } catch(Exception e) {
            report["status"]="failed";report["error"]=e.Message;
            bool running=gui!=null && !gui.HasExited;
            if(changed&&existed&&!running) {
                try {using(var key=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64).OpenSubKey(Key,true))key.SetValue("FirstTime",value,kind);report["flagRecovery"]="Restored original flag.";}
                catch(Exception rollback){report["rollbackError"]=rollback.Message;}
            } else if(changed&&running) report["flagRecovery"]="GUI still running; did not insert a hide flag during initialization.";
        } finally {
            if(before!=null && power!=null && service!=null) {
                try {
                    int[] after=Charge();string afterPower=Power(),afterService=Service();
                    bool same=SameCharge(before,after)&&power==afterPower&&service==afterService;
                    report["chargeAfter"]=new{policy=after[0],limit=after[1]};report["powerAfter"]=afterPower;report["serviceAfter"]=afterService;report["invariantsUnchanged"]=same;
                    if(!same){report["status"]="failed";report["verificationError"]="Charge, power or service state changed; no write-back attempted.";}
                }catch(Exception e){report["status"]="failed";report["verificationError"]=e.Message;}
            }
            if(gui!=null)gui.Dispose();
            File.WriteAllText(Path.Combine(Root,"repair-result.json"),Json.Serialize(report),new UTF8Encoding(false));
            mutex.ReleaseMutex();
        }
        }
        return Convert.ToString(report["status"])=="failed"?1:0;
    }
}
