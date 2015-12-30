using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace ProcessLimiterSrvc
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
            { 
                new Limiter() 
            };
            ServiceBase.Run(ServicesToRun);
        }
    }

    static class Standalone
    {
        private static Limiter limiter;

        static void Main()
        {
            limiter = new Limiter(false);
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            limiter.StartThreaded();
            Group.Speaker.SpeakAsync("Process limiter is watching you");
            limiter.Join();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            limiter.StopThreaded();
        }
    }
}
