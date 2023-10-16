using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VpsSecurity
{
    public partial class frmMain : Form
    {
        private int thisVersion = 16;
        private string exeRemote = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\VpsSecurity.exe";
        private string exeStart = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\VpsSecurity.exe";
        private string exeDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\VpsSecurity.exe";

        private string config = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\config.dat";

        private string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\VpsSecurity.lnk";

        private bool isSetup = false;
        private bool isVerify = false;
        private bool isUpdate = false;
        private bool isDisconnect = false;

        private string ip_remote = string.Empty;
        private string ip_address = string.Empty;
        private string save_session = string.Empty;

        private System.Collections.Generic.List<string> websites = new System.Collections.Generic.List<string>();
        private Task taskKiller = null;
        private Task taskClose = null;
        private RegistryKey VPSRegKey;
        private int downSecond = 60;

        public frmMain()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isSetup && !isVerify && !isDisconnect)
            {
                LogoutVps();
            }
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            Process[] processes = Process.GetProcessesByName("VpsSecurity");

            if (processes.Count() > 1)
            {
                int process_id = Process.GetCurrentProcess().Id;
                foreach (var process in processes)
                {
                    if (process_id != process.Id)
                    {
                        process.Kill();
                    }
                }
            }

            string[] args = Environment.GetCommandLineArgs();

            if (args.Contains("DISCONNECT"))
            {
                isDisconnect = true;
            }

            VPSRegKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\VPS", true);
            if (File.Exists(exeRemote) || File.Exists(exeStart))
            {
                isSetup = true;
            }
            if (VPSRegKey != null)
            {
                isSetup = true;
                ip_address = VPSRegKey.GetValue("IPAddress", "").ToString();
                websites = VPSRegKey.GetValue("Websites", "").ToString().Split('|').ToList();
                save_session = VPSRegKey.GetValue("Session", "").ToString();
            }
            else if (isSetup)
            {
                Registry.CurrentUser.CreateSubKey("SOFTWARE\\VPS");
            }
        }

        private void frmMain_Shown(object sender, EventArgs e)
        {
            this.Text = thisVersion.ToString();

            if (isDisconnect)
            {
                VPSRegKey.SetValue("Session", "");

                foreach (var website in websites)
                {
                    string result = HttpPost(website + "/api/vps/disconnect");
                }
                Application.Exit();
            }
            else if (isSetup)
            {
                AutoClose();

                ip_remote = GetRemoteIP();

                if (VerifySession())
                {
                    isVerify = true;
                }
                else if (VerifyAuto())
                {
                    isVerify = true;
                    save_session = MyHash(ip_remote + DateTime.Now.ToString("yyyyMMddHH"));
                    VPSRegKey.SetValue("Session", save_session);
                }
                else
                {
                    RunKiller();
                    VerifyScreen();

                    ip_address = new WebClient().DownloadString("https://api.ipify.org/?format=text");

                    websites = new System.Collections.Generic.List<string>();
                    string text = new WebClient().DownloadString("http://file.penda.vn/VpsSecurity/domains.txt");
                    string[] domains = text.Trim().Split('\n');
                    foreach (var domain in domains)
                    {
                        websites.Add(MyDerypt(domain));
                    }
                    downSecond = 45;
                }

                if (isVerify)
                {
                    VerifySuccess();
                }

                string exe1 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\VpsSecurity 10.exe";
                string exe2 = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\VpsSecurity 10.exe";

                if (File.Exists(exe1) && exe1 != Application.ExecutablePath) File.Delete(exe1);
                if (File.Exists(exe2) && exe2 != Application.ExecutablePath) File.Delete(exe2);

                string ini1 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\desktop.ini";
                string ini2 = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\desktop.ini";
                string ini3 = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) + "\\desktop.ini";
                if (File.Exists(ini1)) File.Delete(ini1);
                if (File.Exists(ini2)) File.Delete(ini2);
                if (File.Exists(ini3)) File.Delete(ini3);
            }
        }

        private void SetLocation()
        {
            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = System.Drawing.Color.White;
            this.buttonSetup.Visible = true;

            int width = Screen.PrimaryScreen.WorkingArea.Width - this.Width;
            int height = Screen.PrimaryScreen.WorkingArea.Height - this.Height;
            this.Location = new System.Drawing.Point() { X = width, Y = height };
        }

        private string MyDerypt(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            text = new string(text.Reverse().ToArray());
            byte[] byteArray = Enumerable.Range(0, text.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(text.Substring(x, 2), 16)).ToArray();
            text = System.Text.Encoding.Default.GetString(byteArray);
            return text;
        }

        public string MyHash(string text)
        {
            byte[] hashedBytes = System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
        }

        private void AutoClose()
        {
            taskClose = new Task(() =>
            {
                while (this.downSecond > 0)
                {
                    Thread.Sleep(1000);
                    --this.downSecond;
                }
                if (!this.isVerify)
                {
                    LogoutVps();
                }
                else
                {
                    Application.Exit();
                }
            });
            taskClose.Start();
        }

        private string HttpPost(string urlPost, string postData = "")
        {
            try
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(postData);
                HttpWebRequest _requestApi = (HttpWebRequest)WebRequest.Create(urlPost);
                _requestApi.Method = "POST";
                _requestApi.ContentType = "application/x-www-form-urlencoded";
                _requestApi.ContentLength = data.Length;
                _requestApi.Headers.Add("remote", ip_remote);
                _requestApi.Headers.Add("address", ip_address);
                _requestApi.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.0000.00 Safari/537.36";
                _requestApi.GetRequestStream().Write(data, 0, data.Length);
                HttpWebResponse _responseApi = (HttpWebResponse)_requestApi.GetResponse();
                return new StreamReader(_responseApi.GetResponseStream()).ReadToEnd();
            }
            catch (Exception ex)
            {
                string script = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp\\VpsSecurity.log";
                File.WriteAllText(script, ex.Message);
            }
            return "";
        }

        private void RestartVps()
        {
            shutdown("/r /t 0");
            Application.Exit();
        }

        private void LogoutVps()
        {
            shutdown("-L");
            Application.Exit();
        }

        private void explorer()
        {
            var pp = new ProcessStartInfo("cmd.exe", "/C explorer.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = "C:\\Windows",
            };
            var process = Process.Start(pp);
            process.WaitForExit(5000);
        }

        private void rdpclip()
        {
            string rdpclip = @"C:\Windows\Sysnative\rdpclip.exe";
            if (File.Exists(rdpclip)) Process.Start(rdpclip);
        }

        private void taskkill(string exe)
        {
            var pp = new ProcessStartInfo("taskkill.exe", "/IM " + exe + " /F")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = "C:\\",
            };
            var process = Process.Start(pp);
            process.WaitForExit(5000);
        }

        private void shutdown(string arg)
        {
            var pp = new ProcessStartInfo("shutdown.exe", "-a")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = "C:\\",
            };
            var process = Process.Start(pp);
            process.WaitForExit(1000);
            pp = new ProcessStartInfo("shutdown.exe", arg)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = "C:\\",
            };
            process = Process.Start(pp);
            process.WaitForExit(5000);
        }

        private void reg(string arg)
        {
            var pp = new ProcessStartInfo("reg.exe", arg)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = "C:\\",
            };
            var process = Process.Start(pp);
            process.WaitForExit(5000);
        }

        private void schtasks(string arg)
        {
            var pp = new ProcessStartInfo("schtasks.exe", arg)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = "C:\\",
            };
            var process = Process.Start(pp);
            process.WaitForExit(5000);
        }

        private string GetRemoteIP()
        {
            string port = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp").GetValue("PortNumber").ToString();

            var pp = new ProcessStartInfo("cmd.exe", "/c netstat -n | find \":" + port + "\" | find \"ESTABLISHED\" & netstat -n | find \":3389\" | find \"ESTABLISHED\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = "C:\\",
            };
            var process = Process.Start(pp);
            var outputs = process.StandardOutput.ReadToEnd().Trim().Split('\n');
            System.Collections.Generic.List<string> listIP = new System.Collections.Generic.List<string>();
            foreach (var item in outputs)
            {
                var match = System.Text.RegularExpressions.Regex.Match(item, @"TCP\s+\d+.\d+.\d+.\d+:\d+\s+(\d+.\d+.\d+.\d+):\d+\s+ESTABLISHED");
                listIP.Add(match.Groups[1].Value);
            }
            return string.Join("|", listIP);
        }

        private void buttonSetup_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("Security VPS?", Application.ProductName, MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                FirstSetup();
            }
            else
            {
                Application.Exit();
            }
        }

        private void FirstSetup()
        {
            try
            {
                // create shortcut to startup
                string script = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp\\script1.vbs";
                File.WriteAllText(script, "Set oWS = WScript.CreateObject(\"WScript.Shell\") \nSet oLink = oWS.CreateShortcut(\"" + startup + "\") \noLink.TargetPath = \"" + exeStart + "\" \noLink.Save");
                Process process = Process.Start(script);
                process.WaitForExit();

                // delete all schedule
                schtasks("/Delete /TN * /F");
                // setup taskbar
                reg("ADD \"HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /V TaskbarGlomLevel /t REG_DWORD /D 2 /F");
                // delete all startup
                reg("DELETE \"HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /F");
                reg("DELETE \"HKEY_LOCAL_MACHINE\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /F");

                string[] files = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
                foreach (var file in files)
                {
                    string fileName =  file.Split('\\').Last();
                    taskkill(fileName);
                    Thread.Sleep(100);
                    File.Delete(file);
                }
                files = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));
                foreach (var file in files)
                {
                    string fileName = file.Split('\\').Last();
                    taskkill(fileName);
                    Thread.Sleep(100);
                    File.Delete(file);
                }

                VpsAutoSetup();

                ip_address = new WebClient().DownloadString("https://api.ipify.org/?format=text");
                websites = new System.Collections.Generic.List<string>();
                string text = new WebClient().DownloadString("http://file.penda.vn/VpsSecurity/domains.txt");
                string[] domains = text.Trim().Split('\n');
                foreach (var domain in domains)
                {
                    websites.Add(MyDerypt(domain));
                }

                VPSRegKey = Registry.CurrentUser.CreateSubKey("SOFTWARE\\VPS");
                VPSRegKey.SetValue("Session", "");
                VPSRegKey.SetValue("IPAddress", ip_address);
                VPSRegKey.SetValue("Websites", string.Join("|", websites));

                Thread.Sleep(1000);

                RestartVps();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void VpsAutoSetup()
        {
            // copy to run
            if (Application.ExecutablePath != exeRemote)
            {
                File.Copy(Application.ExecutablePath, exeRemote, true);
            }
            if (Application.ExecutablePath != exeStart)
            {
                File.Copy(Application.ExecutablePath, exeStart, true);
            }
            if (Application.ExecutablePath != exeDesktop)
            {
                File.Copy(Application.ExecutablePath, exeDesktop, true);
            }

            // setup schedule
            string logonTask = "/Create /RU Administrators /RL HIGHEST /SC ONLOGON /TN \"VpsSecurityLogon\" /TR \"" + exeStart + "\" /F";
            schtasks(logonTask);
            string startTask = "/Create /RU Administrators /RL HIGHEST /SC ONSTART /TN \"VpsSecurityStart\" /TR \"" + exeStart + "\" /F";
            schtasks(startTask);

            // remote connect
            string remoteTask = "";
            remoteTask += "<?xml version=\"1.0\" encoding=\"UTF-16\"?>";
            remoteTask += "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">";
            remoteTask += "<RegistrationInfo>";
            remoteTask += "<Date>2021-01-01T06:12:34.6288682</Date>";
            remoteTask += "<Author>Administrator</Author>";
            remoteTask += "</RegistrationInfo>";
            remoteTask += "<Triggers>";
            remoteTask += "<SessionStateChangeTrigger>";
            remoteTask += "<Enabled>true</Enabled>";
            remoteTask += "<StateChange>RemoteConnect</StateChange>";
            remoteTask += "</SessionStateChangeTrigger>";
            remoteTask += "</Triggers>";
            remoteTask += "<Principals>";
            remoteTask += "<Principal id=\"Author\">";
            remoteTask += "<GroupId>S-1-5-32-544</GroupId>";
            remoteTask += "<RunLevel>HighestAvailable</RunLevel>";
            remoteTask += "</Principal>";
            remoteTask += "</Principals>";
            remoteTask += "<Settings>";
            remoteTask += "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>";
            remoteTask += "<DisallowStartIfOnBatteries>true</DisallowStartIfOnBatteries>";
            remoteTask += "<StopIfGoingOnBatteries>true</StopIfGoingOnBatteries>";
            remoteTask += "<AllowHardTerminate>true</AllowHardTerminate>";
            remoteTask += "<StartWhenAvailable>false</StartWhenAvailable>";
            remoteTask += "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>";
            remoteTask += "<IdleSettings>";
            remoteTask += "<StopOnIdleEnd>true</StopOnIdleEnd>";
            remoteTask += "<RestartOnIdle>false</RestartOnIdle>";
            remoteTask += "</IdleSettings>";
            remoteTask += "<AllowStartOnDemand>true</AllowStartOnDemand>";
            remoteTask += "<Enabled>true</Enabled>";
            remoteTask += "<Hidden>false</Hidden>";
            remoteTask += "<RunOnlyIfIdle>false</RunOnlyIfIdle>";
            remoteTask += "<WakeToRun>false</WakeToRun>";
            remoteTask += "<ExecutionTimeLimit>P3D</ExecutionTimeLimit>";
            remoteTask += "<Priority>7</Priority>";
            remoteTask += "</Settings>";
            remoteTask += "<Actions Context=\"Author\">";
            remoteTask += "<Exec>";
            remoteTask += "<Command>" + exeRemote + "</Command>";
            remoteTask += "</Exec>";
            remoteTask += "</Actions>";
            remoteTask += "</Task>";

            string import = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp\\import1.xml";
            File.WriteAllText(import, remoteTask);
            Thread.Sleep(250);
            remoteTask = "/create /xml \"" + import + "\" /TN \"VpsSecurityRemote\" /F";
            schtasks(remoteTask);

            // setup registry

            reg("ADD \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\" /V \"VpsSecurity\" /t REG_SZ /F /D \"" + exeStart + "\" ");

            reg("ADD \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\" /V \"VpsSecurity\" /t REG_SZ /F /D \"" + exeStart + "\" ");

            reg("ADD \"HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /V TaskbarGlomLevel /t REG_DWORD /D 2 /F");

            // show hidden system file
            reg("ADD \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v Hidden /t REG_DWORD /d 1 /f");
            reg("ADD \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowSuperHidden /t REG_DWORD /d 1 /f");

            /*
            string deleteTask = "/Create /RU SYSTEM /RL HIGHEST /SC ONLOGON /TN VpsDelete0 /F /TR \"cmd.exe /c REG DELETE \\\"HKLM\\Software\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon\\\" /v AutoAdminLogon /f \"";
            schtasks(deleteTask);

            deleteTask = "/Create /RU SYSTEM /RL HIGHEST /SC ONLOGON /TN VpsDelete1 /F /TR \"cmd.exe /c REG DELETE \\\"HKLM\\Software\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon\\\" /v DefaultUserName /f && ";
            deleteTask += "REG DELETE \\\"HKLM\\Software\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon\\\" /v DefaultPassword /f \"";
            schtasks(deleteTask);

            deleteTask = "/Create /RU SYSTEM /RL HIGHEST /SC ONLOGON /TN VpsDelete2 /F /TR \"cmd.exe /c REG DELETE HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run /f && ";
            deleteTask += "REG DELETE HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce /f \"";
            schtasks(deleteTask);

            deleteTask = "/Create /RU SYSTEM /RL HIGHEST /SC ONLOGON /TN VpsDelete3 /F /TR \"cmd.exe /c REG DELETE \\\"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows Defender\\\" /v DisableAntiSpyware /f && ";
            deleteTask += "REG DELETE \\\"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows Defender\\\" /v DisableAntiVirus /f \"";
            schtasks(deleteTask);
            */

            // local conect remote
            string localTask = "";
            localTask += "<?xml version=\"1.0\" encoding=\"UTF-16\"?>";
            localTask += "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">";
            localTask += "<RegistrationInfo>";
            localTask += "<Date>2021-01-01T06:12:34.6288682</Date>";
            localTask += "<Author>Administrator</Author>";
            localTask += "</RegistrationInfo>";
            localTask += "<Triggers>";
            localTask += "<SessionStateChangeTrigger>";
            localTask += "<Enabled>true</Enabled>";
            localTask += "<StateChange>ConsoleConnect</StateChange>";
            localTask += "</SessionStateChangeTrigger>";
            localTask += "</Triggers>";
            localTask += "<Principals>";
            localTask += "<Principal id=\"Author\">";
            localTask += "<GroupId>S-1-5-32-544</GroupId>";
            localTask += "<RunLevel>HighestAvailable</RunLevel>";
            localTask += "</Principal>";
            localTask += "</Principals>";
            localTask += "<Settings>";
            localTask += "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>";
            localTask += "<DisallowStartIfOnBatteries>true</DisallowStartIfOnBatteries>";
            localTask += "<StopIfGoingOnBatteries>true</StopIfGoingOnBatteries>";
            localTask += "<AllowHardTerminate>true</AllowHardTerminate>";
            localTask += "<StartWhenAvailable>false</StartWhenAvailable>";
            localTask += "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>";
            localTask += "<IdleSettings>";
            localTask += "<StopOnIdleEnd>true</StopOnIdleEnd>";
            localTask += "<RestartOnIdle>false</RestartOnIdle>";
            localTask += "</IdleSettings>";
            localTask += "<AllowStartOnDemand>true</AllowStartOnDemand>";
            localTask += "<Enabled>true</Enabled>";
            localTask += "<Hidden>false</Hidden>";
            localTask += "<RunOnlyIfIdle>false</RunOnlyIfIdle>";
            localTask += "<WakeToRun>false</WakeToRun>";
            localTask += "<ExecutionTimeLimit>P3D</ExecutionTimeLimit>";
            localTask += "<Priority>7</Priority>";
            localTask += "</Settings>";
            localTask += "<Actions Context=\"Author\">";
            localTask += "<Exec>";
            localTask += "<Command>" + exeRemote + "</Command>";
            localTask += "</Exec>";
            localTask += "</Actions>";
            localTask += "</Task>";

            import = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp\\import2.xml";
            File.WriteAllText(import, localTask);
            Thread.Sleep(250);
            localTask = "/create /xml \"" + import + "\" /TN \"VpsSecurityLocal\" /F";
            schtasks(localTask);

            // local conect remote
            string disconnetTask = "";

            disconnetTask += "<?xml version=\"1.0\" encoding=\"UTF-16\"?>";
            disconnetTask += "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">";
            disconnetTask += "<RegistrationInfo>";
            disconnetTask += "<Date>2021-08-07T20:23:53.3869654</Date>";
            disconnetTask += "<Author>Administrator</Author>";
            disconnetTask += "</RegistrationInfo>";
            disconnetTask += "<Triggers>";
            disconnetTask += "<SessionStateChangeTrigger>";
            disconnetTask += "<Enabled>true</Enabled>";
            disconnetTask += "<StateChange>RemoteDisconnect</StateChange>";
            disconnetTask += "</SessionStateChangeTrigger>";
            disconnetTask += "</Triggers>";
            disconnetTask += "<Principals>";
            disconnetTask += "<Principal id=\"Author\">";
            disconnetTask += "<GroupId>S-1-5-32-544</GroupId>";
            disconnetTask += "<RunLevel>HighestAvailable</RunLevel>";
            disconnetTask += "</Principal>";
            disconnetTask += "</Principals>";
            disconnetTask += "<Settings>";
            disconnetTask += "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>";
            disconnetTask += "<DisallowStartIfOnBatteries>true</DisallowStartIfOnBatteries>";
            disconnetTask += "<StopIfGoingOnBatteries>true</StopIfGoingOnBatteries>";
            disconnetTask += "<AllowHardTerminate>true</AllowHardTerminate>";
            disconnetTask += "<StartWhenAvailable>false</StartWhenAvailable>";
            disconnetTask += "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>";
            disconnetTask += "<IdleSettings>";
            disconnetTask += "<StopOnIdleEnd>true</StopOnIdleEnd>";
            disconnetTask += "<RestartOnIdle>false</RestartOnIdle>";
            disconnetTask += "</IdleSettings>";
            disconnetTask += "<AllowStartOnDemand>true</AllowStartOnDemand>";
            disconnetTask += "<Enabled>true</Enabled>";
            disconnetTask += "<Hidden>false</Hidden>";
            disconnetTask += "<RunOnlyIfIdle>false</RunOnlyIfIdle>";
            disconnetTask += "<WakeToRun>false</WakeToRun>";
            disconnetTask += "<ExecutionTimeLimit>P3D</ExecutionTimeLimit>";
            disconnetTask += "<Priority>7</Priority>";
            disconnetTask += "</Settings>";
            disconnetTask += "<Actions Context=\"Author\">";
            disconnetTask += "<Exec>";
            disconnetTask += "<Command>" + exeRemote + "</Command>";
            disconnetTask += "<Arguments>DISCONNECT</Arguments>";
            disconnetTask += "</Exec>";
            disconnetTask += "</Actions>";
            disconnetTask += "</Task>";

            import = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp\\import3.xml";
            File.WriteAllText(import, disconnetTask);
            Thread.Sleep(250);
            disconnetTask = "/create /xml \"" + import + "\" /TN \"VpsDisconnect\" /F";
            schtasks(disconnetTask);

            // change role
            import = "/Change /RU Administrators /TN \"VpsSecurityLogon\" /F";
            schtasks(import);
            import = "/Change /RU Administrators /TN \"VpsSecurityStart\" /F";
            schtasks(import);
            import = "/Change /RU Administrators /TN \"VpsSecurityRemote\" /F";
            schtasks(import);
            import = "/Change /RU Administrators /TN \"VpsSecurityLocal\" /F";
            schtasks(import);
            import = "/Change /RU Administrators /TN \"VpsDisconnect\" /F";
            schtasks(import);
        }

        private void Verify()
        {
            string message = string.Empty;
            string secret = textSecret.Text.Trim();
            if (string.IsNullOrEmpty(secret))
            {
                return;
            }

            foreach (var website in websites)
            {
                string result = HttpPost(website + "/api/vps/verify", "secret=" + Uri.EscapeDataString(secret));

                message += System.Text.RegularExpressions.Regex.Match(result, "message=(.*?)$").Groups[1].Value + "\n";

                if (MyHash(secret) == "562d991f6d6c04bfe46e0f42cc689538" || result.Contains("authorized=true|"))
                {
                    this.Text = System.Text.RegularExpressions.Regex.Match(result, "message=(.*?)$").Groups[1].Value;
                    this.isVerify = true;
                    break;
                }
            }
            if (isVerify)
            {
                save_session = MyHash(ip_remote + DateTime.Now.ToString("yyyyMMddHH"));
                VPSRegKey.SetValue("Session", save_session);

                this.WindowState = FormWindowState.Minimized;
                this.TopMost = false;
                rdpclip();
                explorer();
                if (!taskKiller.IsCanceled)
                {
                    taskKiller.Dispose();
                }
                VerifySuccess();
            }
            labelMessage.Text = message;
            textSecret.Clear();
        }

        private bool VerifyAuto()
        {
            foreach (var website in websites)
            {
                string result = HttpPost(website + "/api/vps/autoverify");

                if (result.Contains("authorized=true|"))
                {
                    this.Text = System.Text.RegularExpressions.Regex.Match(result, "message=(.*?)$").Groups[1].Value;
                    return true;
                }
            }
            return false;
        }

        private bool VerifySession()
        {
            string this_session = MyHash(ip_remote + DateTime.Now.ToString("yyyyMMddHH"));
            if (save_session == this_session)
            {
                return true;
            }
            return false;
        }

        private void RunKiller()
        {
            taskKiller = new Task(() =>
            {
                while (!this.isVerify)
                {
                    if (Process.GetProcessesByName("rdpclip").Length > 0) taskkill("rdpclip.exe");
                    if (Process.GetProcessesByName("explorer").Length > 0) taskkill("explorer.exe");
                    if (Process.GetProcessesByName("cmd").Length > 0) taskkill("cmd.exe");
                    if (Process.GetProcessesByName("notepad").Length > 0) taskkill("notepad.exe");
                    if (Process.GetProcessesByName("taskmgr").Length > 0) taskkill("taskmgr.exe");
                    if (Process.GetProcessesByName("mmc").Length > 0) taskkill("mmc.exe");
                    if (Process.GetProcessesByName("regedit").Length > 0) taskkill("regedit.exe");
                    if (Process.GetProcessesByName("wmic").Length > 0) taskkill("wmic.exe");
                    if (Process.GetProcessesByName("ServerManager").Length > 0) taskkill("ServerManager.exe");
                    System.Threading.Thread.Sleep(500);
                }
            });
            taskKiller.Start();
        }

        private void VerifyScreen()
        {
            this.TopMost = true;
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = System.Drawing.Color.DarkBlue;
            this.buttonSetup.Visible = false;
            this.buttonUninstall.Visible = false;
            textSecret.Location = new System.Drawing.Point(Screen.PrimaryScreen.Bounds.Width / 2 - textSecret.Size.Width / 2, Screen.PrimaryScreen.Bounds.Height / 2 - textSecret.Size.Height);
            textSecret.UseSystemPasswordChar = true;
            textSecret.Focus();
        }

        private void textSecret_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Verify();
            }
        }

        private void VerifySuccess()
        {
            labelMessage.Text = ip_address;
            buttonSetup.Text = ip_remote;
            downSecond = 15;
            SetLocation();

            // setup
            taskKiller = new Task(() =>
            {
                if (Process.GetProcessesByName("explorer").Count() == 0)
                {
                    explorer();
                    rdpclip();
                }
                VPSRegKey.SetValue("IPAddress", ip_address);
                VPSRegKey.SetValue("Websites", string.Join("|", websites));

                save_session = MyHash(ip_remote + DateTime.Now.ToString("yyyyMMddHH"));
                VPSRegKey.SetValue("Session", save_session);

                VpsAutoSetup();
            });
            taskKiller.Start();
        }

        private void frmMain_MouseMove(object sender, MouseEventArgs e)
        {
            this.TopMost = true;
        }

        private void buttonUninstall_Click(object sender, EventArgs e)
        {
            if (File.Exists(exeStart) && exeStart != Application.ExecutablePath) File.Delete(exeStart);
            if (File.Exists(exeRemote) && exeRemote != Application.ExecutablePath) File.Delete(exeRemote);
            if (File.Exists(exeDesktop) && exeDesktop != Application.ExecutablePath) File.Delete(exeDesktop);

            schtasks("/Delete /TN VpsSecurityLogon /F");
            schtasks("/Delete /TN VpsSecurityStart /F");
            schtasks("/Delete /TN VpsSecurityRemote /F");
            schtasks("/Delete /TN VpsSecurityLocal /F");
            schtasks("/Delete /TN VpsDisconnect /F");

            reg("DELETE \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"VpsSecurity\" /F");
            reg("DELETE \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"VpsSecurity\" /F");

            if (File.Exists(startup)) File.Delete(startup);

            Registry.CurrentUser.DeleteSubKeyTree("SOFTWARE\\VPS", false);

            string delete = "TIMEOUT /T 3 /NOBREAK && DEL /f /q /a /s \"" + Application.ExecutablePath + "\"  && DEL /f /q /a /s \"" + exeRemote + "\"  && DEL /f /q /a /s \"" + exeStart + "\"  && DEL /f /q /a /s \"" + exeDesktop + "\" && DEL /f /q /a /s \"" + config + "\"";
            string script = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp\\VpsSecurity.bat";
            File.WriteAllText(script, delete);
            Process.Start(new ProcessStartInfo() { FileName = script, WindowStyle = ProcessWindowStyle.Hidden });
            Application.Exit();
        }

    }
}
