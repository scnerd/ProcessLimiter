using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProcessLimiterSrvc;
using TextMenu;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.ServiceProcess;

namespace ProcessLimiter_2
{
    class Program
    {
        private static List<Group> groups;
        private static Limiter limiter;
        private static Menu editGroupMenu, groupMenu, procMenu;

        static void Main(string[] args)
        {
            limiter = new Limiter(false);
            LoadGroups();

            Delegate nullAction = new Action(() => { });

            editGroupMenu = new Menu(Console.Out, Console.In, true);
            Action<Group> initEditGroupMenu = new Action<Group>((g) =>
                {
                    editGroupMenu.Clear();
                    editGroupMenu.AddItem(new MenuLabel("=== Editing group: " + g.Name + " ==="));
                    editGroupMenu.AddItem('l', "Display group summary", new Action(() => dispGroup(g)));
                    editGroupMenu.AddItem('p', "Add process to group", new Action(() => addProcToGroup(g)));
                    editGroupMenu.AddItem('r', "Remove process from group", new Action(() => rmvProcFromGroup(g)));
                    editGroupMenu.AddItem('t', "Set group time limit", new Action(() => setGroupTimeLimit(g)));
                    editGroupMenu.AddItem('a', "Set group start time", new Action(() => setGroupStartTime(g)));
                    editGroupMenu.AddItem('b', "Set group end time", new Action(() => setGroupEndTime(g)));
                    editGroupMenu.AddItem('s', "Show currently running processes", new Action(() => dispGroupRunningProcs(g)));
                    editGroupMenu.AddItem('u', "Update the group status", new Action(() => updateGroup(g)));
                    editGroupMenu.AddItem('q', "Finish editing", new Action(() => { SaveGroups(); editGroupMenu.CloseSelf(); }));
                });

            groupMenu = new Menu(Console.Out, Console.In, true);
            Action initGroupMenu = new Action(() =>
                {
                    LoadGroups();
                    groupMenu.Clear();
                    groupMenu.AddItem('l', "List groups", new Action(listGroups));
                    groupMenu.AddItem('a', "Add group", new Action(() => { initEditGroupMenu(addGroup()); editGroupMenu.Run(); }));
                    groupMenu.AddItem('e', "Edit group", new Action(() =>
                    {
                        for (int i = 0; i < groups.Count; i++)
                        {
                            groupMenu.PrintLine(i + ". " + groups[i].Name);
                        }
                        int g = groupMenu.RequestIntInRange(0, groups.Count, -1);
                        initEditGroupMenu(groups[g]);
                        editGroupMenu.Run();
                    }));
                    groupMenu.AddItem('d', "Delete group", new Action(rmvGroup));
                    groupMenu.AddItem('q', "Return to main menu", groupMenu.CloseSelf);
                    SaveGroups();
                });

            procMenu = new Menu(Console.Out, Console.In, true);
            Action initProcMenu = new Action(() =>
            {
                procMenu.Clear();
                procMenu.AddItem('s', "Show available processes", new Action(dispRunningProcs));
                procMenu.AddItem('l', "List used processes", new Action(listProcs));
                procMenu.AddItem('q', "Return to main menu", procMenu.CloseSelf);
            });

            Menu mainMenu = new Menu(Console.Out, Console.In, true);
            mainMenu.AddItem('t', "Check status of limiter service", new Action(() => mainMenu.PrintLine(limiter.Status)));
            mainMenu.AddItem('s', "Start limiter service", new Action(limiter.StartThreaded));
            mainMenu.AddItem('x', "Stop limiter service", new Action(limiter.StopThreaded));
            mainMenu.AddItem('r', "Refresh limiter service settings", new Action(() => limiter.Command(LIMITER_COMMAND.REFRESH)));
            mainMenu.AddItem('g', "Edit group settings", new Action(() => { initGroupMenu(); groupMenu.Run(); }));
            mainMenu.AddItem('p', "Edit process settings", new Action(() => { initProcMenu(); procMenu.Run(); }));
            mainMenu.AddItem('q', "Quit Process Limiter Manager", mainMenu.CloseSelf);

            limiter.StartThreaded();
            mainMenu.Run();
            limiter.StopThreaded();
        }

        private static void setGroupEndTime(Group g)
        {
            int h = editGroupMenu.RequestIntInRange(0, 24, -1, Message: "Hour:");
            if (h != -1)
                g.EndTime = new TimeSpan(h,
                    editGroupMenu.RequestIntInRange(0, 60, null, Message: "Minute:"), 0);
            else
                g.EndTime = null;

            editGroupMenu.PrintLine(h == -1 ? "End time cleared" : string.Format("End time set to {0}:{1}", g.EndTime.Value.Hours, g.EndTime.Value.Minutes));
        }

        private static void setGroupStartTime(Group g)
        {
            int h = editGroupMenu.RequestIntInRange(0, 24, -1, Message: "Hour:");
            if (h != -1)
                g.StartTime = new TimeSpan(h,
                    editGroupMenu.RequestIntInRange(0, 60, null, Message: "Minute:"), 0);
            else
                g.StartTime = null;

            editGroupMenu.PrintLine(h == -1 ? "Start time cleared" : string.Format("Start time set to {0}:{1}", g.StartTime.Value.Hours, g.StartTime.Value.Minutes));
        }

        private static void setGroupTimeLimit(Group g)
        {
            int h = editGroupMenu.RequestIntInRange(0, 24, -1, Message: "Hours:");
            if (h != -1)
                g.TimeLimit = new TimeSpan(h,
                    editGroupMenu.RequestIntInRange(0, 60, null, Message: "Minutes:"), 0);
            else
                g.TimeLimit = null;

            editGroupMenu.PrintLine(h == -1 ? "Time limit cleared" : string.Format("Time limit set to {0}:{1}", g.TimeLimit.Value.Hours, g.TimeLimit.Value.Minutes));
        }

        private static void rmvProcFromGroup(Group g)
        {
            for (int i = 0; i < g.ProcessNames.Count; i++)
            {
                editGroupMenu.PrintLine(i + ". " + g.ProcessNames[i]);
            }
            int ind = editGroupMenu.RequestIntInRange(0, g.ProcessNames.Count, -1);
            if(ind >= 0)
                g.ProcessNames.RemoveAt(ind);
        }

        private static void addProcToGroup(Group g)
        {
            g.ProcessNames.Add(editGroupMenu.RequestString(Message: "Input the process name (wildcards allowed):"));
        }

        private static void dispGroup(Group g)
        {
            editGroupMenu.PrintLine("Name:  " + g.Name);
            editGroupMenu.PrintLine("Limit: " + (g.TimeLimit == null ? "None" : g.TimeLimit.Value.Hours + ":" + g.TimeLimit.Value.Minutes));
            editGroupMenu.PrintLine("Start: " + (g.StartTime == null ? "None" : g.StartTime.Value.Hours + ":" + g.StartTime.Value.Minutes));
            editGroupMenu.PrintLine("End:   " + (g.EndTime == null ? "None" : g.EndTime.Value.Hours + ":" + g.EndTime.Value.Minutes));
            foreach (var proc in g.ProcessNames)
            {
                editGroupMenu.PrintLine("Proc:  " + proc);
            }
        }

        private static void dispGroupRunningProcs(Group g)
        {
            foreach (var proc in g.RunningProcs)
                editGroupMenu.PrintLine("-> " + proc.ProcessName);
        }

        private static void updateGroup(Group g)
        {
            g.Update();
        }

        private static void rmvGroup()
        {
            for (int i = 0; i < groups.Count; i++)
            {
                groupMenu.PrintLine(i + ". " + groups[i].Name);
            }
            int ind = groupMenu.RequestIntInRange(0, groups.Count, -1);
            if (ind >= 0)
                groups.RemoveAt(ind);
        }

        private static Group addGroup()
        {
            Group g = new Group(groupMenu.RequestString(Message: "Group Name:"));
            groups.Add(g);
            return g;
        }

        private static void listGroups()
        {
            foreach (var g in groups)
            {
                groupMenu.PrintLine(g.Name);
            }
        }

        private static void dispRunningProcs()
        {
            foreach (var procName in System.Diagnostics.Process.GetProcesses().Select(p => p.ProcessName).Distinct())
            {
                procMenu.PrintLine(procName);
            }
        }

        private static void listProcs()
        {
            foreach (var proc in groups.SelectMany(g => g.ProcessNames).Distinct())
                procMenu.PrintLine(proc);
        }

        private static void LoadGroups()
        {
            if (File.Exists(Limiter.CONFIG_FILE))
            {
                try
                {
                    using (var stream = File.OpenRead(Limiter.CONFIG_FILE))
                    {
                        groups = (List<Group>)new BinaryFormatter().Deserialize(stream);
                    }
                    return;
                }
                catch { }
            }
            groups = new List<Group>();
            SaveGroups();
        }

        private static void SaveGroups()
        {
            try
            {
                using (var f = File.Create(Limiter.CONFIG_FILE))
                {
                    new BinaryFormatter().Serialize(f, groups);
                }
            }
            catch (Exception ex)
            {
                File.Delete(Limiter.CONFIG_FILE);
                throw ex;
            }
        }
    }
}
