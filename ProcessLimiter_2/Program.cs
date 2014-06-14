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
            limiter = new Limiter();
            LoadGroups();

            Delegate nullAction = new Action(() => { });

            editGroupMenu = new Menu(Console.Out, Console.In, true);
            Action<Group> initEditGroupMenu = new Action<Group>((g) =>
                {
                    editGroupMenu.Clear();
                    editGroupMenu.AddItem('l', "Display group summary", new Action(() => dispGroup(g)));
                    editGroupMenu.AddItem('p', "Add process to group", new Action(() => addProcToGroup(g)));
                    editGroupMenu.AddItem('r', "Remove process from group", new Action(() => rmvProcFromGroup(g)));
                    editGroupMenu.AddItem('t', "Set group time limit", new Action(() => setGroupTimeLimit(g)));
                    editGroupMenu.AddItem('a', "Set group start time", new Action(() => setGroupStartTime(g)));
                    editGroupMenu.AddItem('b', "Set group end time", new Action(() => setGroupEndTime(g)));
                    editGroupMenu.AddItem('q', "Finish editing", editGroupMenu.CloseSelf);
                });

            groupMenu = new Menu(Console.Out, Console.In, true);
            Action initGroupMenu = new Action(() =>
                {
                    groupMenu.Clear();
                    groupMenu.AddItem('l', "List groups", new Action(listGroups));
                    groupMenu.AddItem('a', "Add group", new Action(addGroup));
                    groupMenu.AddItem('e', "Edit group", new Action(() =>
                    {
                        int g = groupMenu.RequestIntInRange(0, groups.Count, -1);
                        initEditGroupMenu(groups[g]);
                        editGroupMenu.Run();
                    }));
                    groupMenu.AddItem('d', "Delete group", new Action(rmvGroup));
                    groupMenu.AddItem('q', "Return to main menu", groupMenu.CloseSelf);
                });

            procMenu = new Menu(Console.Out, Console.In, true);
            Action initProcMenu = new Action(() =>
            {
                procMenu.Clear();
                procMenu.AddItem('s', "Show available processes", new Action(dispRunningProcs));
                procMenu.AddItem('l', "List processes", new Action(listProcs));
                procMenu.AddItem('a', "Add process", new Action(addProc));
                procMenu.AddItem('d', "Remove process", new Action(rmvProc));
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

        private static void setGroupEndTime(Group obj)
        {
            throw new NotImplementedException();
        }

        private static void setGroupStartTime(Group obj)
        {
            throw new NotImplementedException();
        }

        private static void setGroupTimeLimit(Group obj)
        {
            throw new NotImplementedException();
        }

        private static void rmvProcFromGroup(Group obj)
        {
            throw new NotImplementedException();
        }

        private static void addProcToGroup(Group obj)
        {
            throw new NotImplementedException();
        }

        private static void dispGroup(Group obj)
        {
            throw new NotImplementedException();
        }

        private static void rmvGroup()
        {
            throw new NotImplementedException();
        }

        private static void addGroup()
        {
            throw new NotImplementedException();
        }

        private static void listGroups()
        {
            throw new NotImplementedException();
        }

        private static void dispRunningProcs()
        {
            throw new NotImplementedException();
        }

        private static void rmvProc()
        {
            throw new NotImplementedException();
        }

        private static void addProc()
        {
            throw new NotImplementedException();
        }

        private static void listProcs()
        {
            throw new NotImplementedException();
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
