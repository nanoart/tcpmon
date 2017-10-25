using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace tcpmon
{
    public class NativeFunc
    {
        public static int period = 600; //seconds
        public static dynamic smtp;
        public static dynamic serviceMonitored;

        public static ushort port = 8074;
        public static int threshold = 20;
        public static int state = 8;

        public static void Init()
        {
            Log.Information("Create Log");

            System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
            loadSettings("settings.json");
            Log.Information("Job is executed per {Period} seconds", period);

        }
        public static void loadSettings(string jsonFile)
        {
            //load settings from a json file
            try
            {
                dynamic settings = JObject.Parse(File.ReadAllText(jsonFile));    //must dynamic

                serviceMonitored = settings.service;
                smtp = settings.smtp;

                port = settings.port;
                period = settings.period;
                threshold = settings.threshold;
                state = settings.state;
            }
            catch (Exception ex)
            {
                Log.Error("Exception caught in loadSettings(): {0}", ex.ToString());
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public class MIB_TCPROW
        {
            public int dwState;
            public int dwLocalAddr;
            public int dwLocalPort;
            public int dwRemoteAddr;
            public int dwRemotePort;
        }
        [StructLayout(LayoutKind.Sequential)]
        public class MIB_TCPTABLE
        {
            public int dwNumEntries;
            public MIB_TCPROW[] table;
        }
        [DllImport("Iphlpapi.dll")]
        static extern int GetTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder);
        [DllImport("Iphlpapi.dll")]
        static extern int SendARP(Int32 DestIP, Int32 SrcIP, ref Int64 MacAddr, ref Int32 PhyAddrLen);
        [DllImport("Ws2_32.dll")]
        static extern Int32 inet_addr(string ipaddr);
        [DllImport("Ws2_32.dll")]
        static extern ushort ntohs(ushort netshort);
        //SendArp获取MAC地址
        public static string GetMacAddress(string macip)
        {
            StringBuilder strReturn = new StringBuilder();
            try
            {
                Int32 remote = inet_addr(macip);
                Int64 macinfo = new Int64();
                Int32 length = 6;
                SendARP(remote, 0, ref macinfo, ref length);
                string temp = System.Convert.ToString(macinfo, 16).PadLeft(12, '0').ToUpper();
                int x = 12;
                for (int i = 0; i < 6; i++)
                {
                    if (i == 5) { strReturn.Append(temp.Substring(x - 2, 2)); }
                    else { strReturn.Append(temp.Substring(x - 2, 2) + ":"); }
                    x -= 2;
                }
                return strReturn.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
        public static bool IsHostAlive(string strHostIP)
        {
            string strHostMac = GetMacAddress(strHostIP);
            return !string.IsNullOrEmpty(strHostMac);
        }
        public static MIB_TCPTABLE GetTcpTableInfo()
        {
            IntPtr hTcpTableData = IntPtr.Zero;
            int iBufferSize = 0;
            MIB_TCPTABLE tcpTable = new MIB_TCPTABLE();
            List<MIB_TCPROW> lstTcpRows = new List<MIB_TCPROW>();
            GetTcpTable(hTcpTableData, ref iBufferSize, false);
            hTcpTableData = Marshal.AllocHGlobal(iBufferSize);
            int iTcpRowLen = Marshal.SizeOf(typeof(MIB_TCPROW));
            int aryTcpRowLength = (int)Math.Ceiling((double)(iBufferSize - sizeof(int)) / iTcpRowLen);
            GetTcpTable(hTcpTableData, ref iBufferSize, false);
            for (int i = 0; i < aryTcpRowLength; i++)
            {
                IntPtr hTempTableRow = new IntPtr(hTcpTableData.ToInt64() + 4 + i * iTcpRowLen);
                MIB_TCPROW tcpRow = new MIB_TCPROW();
                tcpRow.dwLocalAddr = 0;
                tcpRow.dwLocalPort = 0;
                tcpRow.dwRemoteAddr = 0;
                tcpRow.dwRemotePort = 0;
                tcpRow.dwState = 0;


                Marshal.PtrToStructure(hTempTableRow, tcpRow);
                lstTcpRows.Add(tcpRow);
            }
            tcpTable.dwNumEntries = lstTcpRows.Count;
            tcpTable.table = new MIB_TCPROW[lstTcpRows.Count];
            lstTcpRows.CopyTo(tcpTable.table);
            return tcpTable;
        }
        public static string GetIpAddress(long ipAddrs)
        {
            try
            {
                System.Net.IPAddress ipAddress = new System.Net.IPAddress(ipAddrs);
                return ipAddress.ToString();
            }
            catch { return ipAddrs.ToString(); }
        }
        public static ushort GetTcpPort(int tcpPort)
        {
            return ntohs((ushort)tcpPort);
        }
        public static bool IsPortBusy(int port)
        {
            MIB_TCPTABLE tcpTableData = GetTcpTableInfo();
            return false;
        }

        public static void Bob()
        {
            Log.Information("get total sockets");
            int total = 0;
            NativeFunc.MIB_TCPTABLE tcpTableData = new NativeFunc.MIB_TCPTABLE();
            tcpTableData = GetTcpTableInfo();
            for (int i = 0; i < tcpTableData.dwNumEntries; i++)
            {
                ushort localPort = GetTcpPort(tcpTableData.table[i].dwLocalPort);
//                ushort localPort = GetTcpPort(tcpTableData.table[i].dwRemotePort);

                if (localPort == port)
                {
                    //https://msdn.microsoft.com/en-us/library/windows/desktop/aa366909(v=vs.85).aspx
                    if (tcpTableData.table[i].dwState == state)
                    {
                        total++;
                    }

                }
            }

            Log.Information("{0} connections which have state = {1}", total, state);

            if (total >= threshold)
                NativeFunc.notifyAdmin();

            if (serviceMonitored.restart.Value)
            {
                Log.Information("Service {0} is configured as restart-able", serviceMonitored.name.Value);
                if(total >= serviceMonitored.condition.Value)
                {
                    Log.Information("Service {0} is going to restart", serviceMonitored.name.Value);
                    int timeoutMilliseconds = (int)(1000 * serviceMonitored.timeout.Value);
                    RestartService(serviceMonitored.name.Value, timeoutMilliseconds);
                }
            }
        }

        public static void RestartService(string serviceName, int timeoutMilliseconds)
        {
            ServiceController service = new ServiceController(serviceName);
            try
            {
                int millisec1 = Environment.TickCount;
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

                // count the rest of the timeout
                int millisec2 = Environment.TickCount;
                timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds - (millisec2 - millisec1));

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            catch (Exception e)
            {
                Log.Error("Could not restart service {0} due to error {1}", serviceName, e);
            }
        }

        public static void notifyAdmin()
        {
            if (!smtp.enabled.Value)
            {
                Log.Information("SMTP is not enabled in settings.json");
                return;
            }

            string mailServer = smtp.server;
            int mailPort = smtp.port;
            SmtpClient smtpClient = new SmtpClient(mailServer, mailPort);

            if (smtp.auth.Value)
            {
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new System.Net.NetworkCredential(smtp.username.Value, smtp.password.Value);
            }
            else
            {
                smtpClient.UseDefaultCredentials = true;
            }



            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtpClient.EnableSsl = smtp.ssl.Value;
            MailMessage mail = new MailMessage();

            //Setting From , To and CC
            mail.From = new MailAddress(smtp.username.Value);

            for (int i = 0; i < smtp.to.Count; i++)
            {
                mail.To.Add(new MailAddress(smtp.to[i].Value));
            }

            mail.Subject = smtp.customize.subject.Value;

            mail.Body = string.Format(smtp.customize.body.Value, System.AppDomain.CurrentDomain.BaseDirectory + "logs");

            try
            {
                smtpClient.Send(mail);
                Log.Information("Admin is notified");
            }
            catch (Exception ex)
            {
                Log.Error("Exception caught in notifyAdmin(): {0}", ex.ToString());
            }


        }

    }
 
}
