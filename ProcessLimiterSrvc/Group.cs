using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;
using System.Reflection;

namespace ProcessLimiterSrvc
{
    [Serializable]
    public class Group
    {
        public Group(string Name)
        { this.Name = Name; }

        public readonly List<string> ProcessNames = new List<string>();
        public TimeSpan? TimeLimit = null;
        public TimeSpan? StartTime = null, EndTime = null;
        public readonly string Name = "";

        public DateTime LastUpdate;
        private TimeSpan? LimitRemaining = null, BoundaryRemaining = null;

        private static Regex MatchWildcard(string SimplePattern)
        {
            return new Regex("^" + Regex.Escape(SimplePattern)
                              .Replace(@"\*", ".*")
                              .Replace(@"\?", ".")
                       + "$", RegexOptions.IgnoreCase);
        }

        public void Initialize()
        {
            LastUpdate = DateTime.Now;
            LimitRemaining = TimeLimit;
            Update();
        }

        public Process[] RunningProcs
        {
            get
            {
                return
                    Process.GetProcesses().Where(p =>
                        ProcessNames.Select(n =>
                            MatchWildcard(n))
                            .Any(r =>
                                r.IsMatch(p.ProcessName))).ToArray();
            }
        }

        public TimeSpan? RemainingTime
        { get { return 
            LimitRemaining == null ? 
            (BoundaryRemaining == null ? null : BoundaryRemaining) :
            (BoundaryRemaining == null ? LimitRemaining :
            LimitRemaining < BoundaryRemaining ? LimitRemaining : BoundaryRemaining); } }

        public void ResetRemainingTime()
        {
            LimitRemaining = TimeLimit;

            TimeSpan CurTime = DateTime.Now.TimeOfDay;
            if (StartTime != null && EndTime != null)
                if (StartTime <= CurTime && CurTime <= EndTime)
                    BoundaryRemaining = EndTime - CurTime;
                else
                    BoundaryRemaining = TimeSpan.Zero;
            else if (StartTime != null)
                BoundaryRemaining = CurTime > StartTime ? TimeSpan.FromDays(1) - CurTime : TimeSpan.Zero;
            else if (EndTime != null && EndTime < CurTime)
                BoundaryRemaining = EndTime - CurTime;
        }

        public void Update()
        {
            TimeSpan NewTime = DateTime.Now.TimeOfDay;
            bool IsNewDay = !DateTime.Now.Date.Equals(LastUpdate.Date);
            var Running = RunningProcs;
            bool NeedToTerminate = false;

            if (Running.Length > 0)
            {
                //Update remaining time
                if (StartTime != null && EndTime != null)
                    if (StartTime <= NewTime && NewTime <= EndTime)
                        BoundaryRemaining = EndTime - NewTime;
                    else
                        BoundaryRemaining = TimeSpan.Zero;
                else if (StartTime != null)
                    BoundaryRemaining = NewTime > StartTime ? TimeSpan.FromDays(1) - NewTime : TimeSpan.Zero;
                else if (EndTime != null && EndTime < NewTime)
                    BoundaryRemaining = EndTime - NewTime;

                if (TimeLimit != null)
                {
                    if (IsNewDay)
                        LimitRemaining = TimeLimit;
                    else if (LimitRemaining != null)
                        LimitRemaining -= NewTime - LastUpdate.TimeOfDay;
                    else
                        LimitRemaining = TimeLimit;
                }

                if (LimitRemaining != null && BoundaryRemaining != null)
                {
                    NeedToTerminate = !(LimitRemaining > TimeSpan.Zero && BoundaryRemaining > TimeSpan.Zero);
                }
                else if (LimitRemaining != null)
                {
                    NeedToTerminate = !(LimitRemaining > TimeSpan.Zero);
                }
                else if (BoundaryRemaining != null)
                {
                    NeedToTerminate = !(BoundaryRemaining > TimeSpan.Zero);
                }

                if (NeedToTerminate)
                {
                    KillAll(Running);
                }
            }
            LastUpdate = DateTime.Now;
        }

        private void KillAll(IEnumerable<Process> Procs)
        {
            foreach (var proc in Procs)
            {
                Task t = new Task(() =>
                {
                    var note = new NotifyIcon();
                    try
                    {
                        proc.Kill();
                        note.BalloonTipTitle = "ProcessLimiter Notice";
                        note.BalloonTipText = "ProcessLimiter has terminated '" + proc.ProcessName + "'";
                    }
                    catch (Win32Exception)
                    {
                        note.BalloonTipTitle = "ProcessLimiter Permission Denied";
                        note.BalloonTipText = "ProcessLimiter denied permission to kill process '" + proc.ProcessName + "'";
                    }
                    catch (InvalidOperationException)
                    { /* Ignore */ return; }
                    note.Text = "ProcessLimiter";
                    note.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("ProcessLimiterSrvc.clock.ico"));
                    note.Visible = true;
                    note.ShowBalloonTip(3000);
                    Application.Run();
                });
                t.Start();
            }
        }

        public void KillAll()
        {
            KillAll(RunningProcs);
        }
    }
}
