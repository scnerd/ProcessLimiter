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

using System.Speech.Synthesis;

namespace ProcessLimiterSrvc
{
    [Serializable]
    public class Group
    {
        [NonSerialized]
        internal static SpeechSynthesizer Speaker = new SpeechSynthesizer();

        public Group(string Name)
        { this.Name = Name; }

        public readonly List<string> ProcessNames = new List<string>();
        public readonly List<string> DirectoryNames = new List<string>(); 
        public TimeSpan? TimeLimit = null;
        public TimeSpan? StartTime = null, EndTime = null;
        public readonly string Name = "";

        [NonSerialized]
        private TimeSpan? TimeRemaining = null;
        [NonSerialized]
        private static TimeSpan? WarningBoundary = TimeSpan.FromMinutes(15);

        [NonSerialized]
        public DateTime LastUpdate;
        [NonSerialized]
        private TimeSpan? LimitRemaining = null, BoundaryRemaining = null;

        private static Regex MatchWildcard(string SimplePattern)
        {
            return new Regex("^" + Regex.Escape(SimplePattern)
                              .Replace(@"\*", ".*")
                              .Replace(@"\?", ".")
                       + "$", RegexOptions.IgnoreCase);
        }

        private static Regex DirectoryWildcard(string SimplePattern)
        {
            return new Regex("^" + Regex.Escape(SimplePattern)
                              .Replace(@"\*", ".*")
                              .Replace(@"\?", "."), RegexOptions.IgnoreCase);
        }

        public void Initialize()
        {
            LastUpdate = DateTime.Now;
            LimitRemaining = TimeLimit;
            Update();
        }

        private bool MatchName(Process p)
        {
            return ProcessNames
                .Select(n => MatchWildcard(n))
                .Any(r => r.IsMatch(p.ProcessName));
        }

        private bool MatchDirectory(Process p)
        {
            return DirectoryNames
                .Select(n => DirectoryWildcard(n))
                .Any(r =>
                {
                    try
                    {
                        return r.IsMatch(p.MainModule.FileName);
                    }

                    catch(Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
                    {
                        return false;
                    }
                });
        }

        public Process[] RunningProcs
        {
            get
            {
                return
                    Process.GetProcesses().Where(p =>
                        MatchName(p) || MatchDirectory(p)).ToArray();
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
            TimeSpan? NewTimeRemaining = null;

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
                else if (EndTime != null)
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

                if (LimitRemaining.HasValue && BoundaryRemaining.HasValue)
                {
                    NewTimeRemaining = LimitRemaining < BoundaryRemaining ? LimitRemaining : BoundaryRemaining;
                }
                else if (LimitRemaining.HasValue)
                {
                    NewTimeRemaining = LimitRemaining;
                }
                else if (BoundaryRemaining.HasValue)
                {
                    NewTimeRemaining = BoundaryRemaining;
                }
                NeedToTerminate = !(NewTimeRemaining > TimeSpan.Zero);
                
                SpeakWarning(TimeRemaining, NewTimeRemaining, Running);

                if (NeedToTerminate)
                {
                    KillAll(Running);
                }
            }
            TimeRemaining = NewTimeRemaining;
            LastUpdate = DateTime.Now;
        }

        private void SpeakWarning(TimeSpan? timeRemaining, TimeSpan? newTimeRemaining, Process[] procs)
        {
            var ProcNames = procs.Select(p => p.ProcessName.Replace(".", " dot ")).ToArray();
            string SayProcs;
            if (ProcNames.Length == 0)
                return;
            else if (ProcNames.Length == 1)
                SayProcs = ProcNames[0];
            else if (ProcNames.Length == 2)
                SayProcs = string.Format("{0} and {1}", ProcNames);
            else
                SayProcs = string.Format("{0} processes", ProcNames.Length);

            string SayTime = "unknown time";
            if (newTimeRemaining.HasValue)
                if (newTimeRemaining > TimeSpan.Zero)
                    SayTime = string.Format("{0} minutes", newTimeRemaining.Value.Minutes);

            if (newTimeRemaining.HasValue)
                if (newTimeRemaining <= TimeSpan.Zero)
                    Speaker.SpeakAsync($"Closing {SayProcs}");
                else if ((!timeRemaining.HasValue || timeRemaining > WarningBoundary) &&
                         WarningBoundary >= newTimeRemaining)
                    Speaker.SpeakAsync($"{SayProcs} will close in {SayTime}");
        }

        private void KillAll(IEnumerable<Process> Procs)
        {
            var Killed = new List<string>();
            foreach (var proc in Procs)
            {
                Task t = new Task(() =>
                {
                    var note = new NotifyIcon();
                    try
                    {
                        proc.Kill();
                        Killed.Add(proc.ProcessName);
                    }
                    catch (Win32Exception)
                    {
                        if (!proc.HasExited)
                        {
                            note.BalloonTipTitle = "ProcessLimiter Permission Denied";
                            note.BalloonTipText = "ProcessLimiter denied permission to kill process '" + proc.ProcessName + "'";
                        }
                    }
                    catch (InvalidOperationException)
                    { /* Ignore */ return; }
                    note.Text = "ProcessLimiter";
                    note.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("ProcessLimiterSrvc.clock.ico"));
                    note.Visible = true;
                    note.ShowBalloonTip(1000);
                    Application.Run();
                });
                t.Start();
            }
            var killedNote = new NotifyIcon();
            killedNote.BalloonTipTitle = "ProcessLimiter Notice";
            killedNote.BalloonTipText = "ProcessLimiter has terminated the following:";
            foreach (var name in Killed)
                killedNote.BalloonTipText += $" '{name}'";
            killedNote.Text = "ProcessLimiter";
            killedNote.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("ProcessLimiterSrvc.clock.ico"));
            killedNote.Visible = true;
            killedNote.ShowBalloonTip(1000);
            Application.Run();
        }

        public void KillAll()
        {
            KillAll(RunningProcs);
        }
    }
}
