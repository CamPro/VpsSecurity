using System;
using System.Collections.Specialized;
using System.IO;
using System.Management;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace VpsClient
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            WebClient client = new WebClient(); 
            string ipaddress = string.Empty;
            bool isRunning = true;
            int second = 30;
            string url = string.Empty;
            string device = GetDevice();
            string filename = "url.txt";
            if (File.Exists(filename))
            {
                url = File.ReadAllText(filename).Trim();
            }
            while (isRunning)
            {
                string response = string.Empty;
                try
                {
                    ipaddress = client.DownloadString("https://api.ipify.org/?format=text");
                }
                catch
                {
                }
                if (string.IsNullOrEmpty(ipaddress))
                {
                    try
                    {
                        response = client.DownloadString("https://api.ipify.org");
                    }
                    catch
                    {
                    }
                }
                if (string.IsNullOrEmpty(ipaddress))
                {
                    try
                    {
                        response = client.DownloadString("https://api.ipgeolocation.io/getip");
                        SimpleJSON.JSONNode node = SimpleJSON.JSONNode.Parse(response);
                        ipaddress = node["ip"].Value;
                    }
                    catch
                    {
                    }
                }
                if (string.IsNullOrEmpty(ipaddress))
                {
                    try
                    {
                        response = client.DownloadString("http://ip-api.com/json");
                        SimpleJSON.JSONNode node = SimpleJSON.JSONNode.Parse(response);
                        ipaddress = node["query"].Value;
                    }
                    catch
                    {
                    }
                }
                if (string.IsNullOrEmpty(ipaddress))
                {
                    try
                    {
                        response = client.DownloadString("https://lumtest.com/myip.json");
                        SimpleJSON.JSONNode node = SimpleJSON.JSONNode.Parse(response);
                        ipaddress = node["ip"].Value;
                    }
                    catch
                    {
                    }
                }
                try
                {
                    client.UploadValues(url, "POST", new NameValueCollection() {
                        { "ip", ipaddress },
                        { "device", device }
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                Thread.Sleep(second * 1000);
            }
        }

        static string GetDevice()
        {
            string code = Environment.MachineName + Environment.UserName + Environment.OSVersion.VersionString;
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select ProcessorId FROM Win32_Processor");
                foreach (ManagementObject info in searcher.Get())
                {
                    code = info.GetPropertyValue("ProcessorId").ToString();
                }
                searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (ManagementObject info in searcher.Get())
                {
                    code += info["SerialNumber"].ToString();
                }
                searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
                foreach (ManagementObject info in searcher.Get())
                {
                    code += info["SerialNumber"].ToString().Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return code;
        }

    }
}
