using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.IO;
using System.Threading;

namespace ProcessLimiterSrvc
{
    public enum LIMITER_COMMAND { SUSPEND = 128, RESUME, REFRESH }

    public partial class Limiter : ServiceBase
    {
        public const string CONFIG_FILE = "config.xml";
        public const int UPDATE_INTERVAL = 5000;

        private ManagementEventWatcher starter, stopper;
        private bool monitoring = true;
        private readonly bool debugMode;
        private Timer timer;

        private ManualResetEventSlim handle = new ManualResetEventSlim();

        List<Group> groups;

        public Limiter(bool ServiceMode = true)
        {
            InitializeComponent();
            ResetLimiterPrefs();
            starter = new ManagementEventWatcher("SELECT TargetInstance FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance isa 'Win32_Process'");
            stopper = new ManagementEventWatcher("SELECT TargetInstance FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance isa 'Win32_Process'");
            starter.EventArrived += (s,e) => onProcessEvent(e);
            stopper.EventArrived += (s,e) => onProcessEvent(e);

            debugMode = !ServiceMode;

            timer = new Timer(UpdateProcesses, this, Timeout.Infinite, UPDATE_INTERVAL);
        }

        internal void Join()
        {
            handle.Wait();
        }

        private void onProcessEvent(EventArrivedEventArgs e)
        {
            UpdateProcesses(this);
        }

        private void UpdateProcesses(object state)
        {
            if (monitoring)
                foreach (var group in groups)
                    group.Update();
        }

        private void ResetLimiterPrefs()
        {
            if (File.Exists(CONFIG_FILE))
            {
                using (var f = File.OpenRead(CONFIG_FILE))
                {
                    groups = (List<Group>)new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Deserialize(f);
                }
                foreach (var g in groups)
                    g.Initialize();
            }
            else
                groups = new List<Group>();
        }



        protected override void OnStart(string[] args)
        {
            handle.Reset();
            monitoring = true;
            timer.Change(0, UPDATE_INTERVAL);
            starter.Start();
            stopper.Start();
        }

        protected override void OnStop()
        {
            monitoring = false;
            timer.Change(Timeout.Infinite, UPDATE_INTERVAL);
            starter.Stop();
            stopper.Stop();
            handle.Set();
        }

        public void StartThreaded()
        {
            //if (debugMode)
            //{
                this.OnStart(null);
            //}
        }

        public void Command(int Command)
        {
            this.OnCustomCommand(Command);
        }

        public void Command(LIMITER_COMMAND Command)
        {
            //if (debugMode)
            //{
                this.Command((int)Command);
            //}
        }

        public void StopThreaded()
        {
            //if (debugMode)
            //{
                this.OnStop();
            //}
        }

        public string Status
        {
            get { return (debugMode ? "Debug: " : "Service: ") + (monitoring ? "Active" : "Disabled"); }
        }

        protected override void OnCustomCommand(int command)
        {
            if (!Enum.GetValues(typeof(LIMITER_COMMAND)).OfType<int>().Contains(command))
                base.OnCustomCommand(command);
            else
                switch ((LIMITER_COMMAND)command)
                {
                    case LIMITER_COMMAND.REFRESH:
                        ResetLimiterPrefs();
                        break;

                    case LIMITER_COMMAND.RESUME:
                        monitoring = true;
                        break;

                    case LIMITER_COMMAND.SUSPEND:
                        monitoring = false;
                        break;
                }
        }
    }
}
