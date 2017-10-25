﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

using Serilog;

namespace tcpmon
{
    public partial class SmartFixService : ServiceBase
    {
        private Timer frequncy = null;
        string strWorkingDirectory;


               
        public SmartFixService()
        {
            InitializeComponent();

        }

        protected override void OnStart(string[] args)
        {
            strWorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);

            NativeFunc.Init();

            frequncy = new Timer();
            frequncy.Interval = NativeFunc.period*1000;   //every 30 secs
            frequncy.Elapsed += new System.Timers.ElapsedEventHandler(this.frequncy_Tick);
            frequncy.Enabled = true;

            NativeFunc.Bob();
        }

        protected override void OnStop()
        {
            frequncy.Enabled = false;
        }

        private void frequncy_Tick(object sender, ElapsedEventArgs e)
        {
            Log.Information("Start to check the tcp socket status");

            NativeFunc.Bob();

            Log.Information("Finish to check the tcp socket status");
        }

    }
}
