using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Text.RegularExpressions;

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
        private TimeSpan? RemainingTime = null;

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

        public void Update()
        {
            TimeSpan NewTime = DateTime.Now.TimeOfDay;
            bool IsNewDay = !DateTime.Now.Date.Equals(LastUpdate.Date);
            var Running = RunningProcs;
            bool NeedToTerminate = false;

            if (Running.Length > 0)
            {
                //Update remaining time
                TimeSpan? LimitRemaining = null, BoundaryRemaining = null;
                if (StartTime != null && EndTime != null)
                    if (StartTime <= NewTime && NewTime <= EndTime)
                        BoundaryRemaining = EndTime - NewTime;
                    else
                        BoundaryRemaining = TimeSpan.Zero;
                else if (StartTime != null && NewTime < StartTime)
                    BoundaryRemaining = TimeSpan.Zero;
                else if (EndTime != null && EndTime < NewTime)
                    BoundaryRemaining = TimeSpan.Zero;

                if (TimeLimit != null)
                {
                    if (IsNewDay)
                        RemainingTime = TimeLimit;
                    else if (RemainingTime != null)
                        RemainingTime -= NewTime - LastUpdate.TimeOfDay;
                    else
                        RemainingTime = TimeLimit;
                    LimitRemaining = RemainingTime;
                }

                if (LimitRemaining != null && BoundaryRemaining != null)
                    NeedToTerminate = !(LimitRemaining > TimeSpan.Zero && BoundaryRemaining > TimeSpan.Zero);
                else if (LimitRemaining != null)
                    NeedToTerminate = !(LimitRemaining > TimeSpan.Zero);
                else if (BoundaryRemaining != null)
                    NeedToTerminate = !(BoundaryRemaining > TimeSpan.Zero);

                if (NeedToTerminate)
                    KillAll(Running);
            }
            LastUpdate = DateTime.Now;
        }

        private void KillAll(IEnumerable<Process> Procs)
        {
            foreach (var proc in Procs)
            {
                Task t = new Task(() => {
                    try
                    { proc.Kill(); }
                    catch (AccessViolationException)
                    {
                        var note = new System.Windows.Forms.NotifyIcon();
                        note.BalloonTipText = "ProcessLimiter denied permission to kill process '" + proc.ProcessName + "'";
                        note.BalloonTipTitle = "ProcessLimiter Permission Denied";
                        note.Visible = true;
                        note.ShowBalloonTip(3000);
                    }
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
