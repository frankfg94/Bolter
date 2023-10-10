using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Windows;
using static Bolter.NonAdmin;

namespace Bolter
{
    public class ProgramToClose : AbstractSecurity
    {
        public readonly string programName;

        public List<(TimeSpan startTime, TimeSpan endTime)> schedules;
        /// <summary>
        /// Indicates day of the weeks where this program won't be automatically closed
        /// </summary>
        public readonly Dictionary<DayOfWeek, bool> noCloseDays;
        private static ManagementEventWatcher watcher;
        private static Thread processWatcherThread;

        // TODO : see to use cancellation Token instead of thread abort and see if relevant

        /// <summary>
        /// Triggered when a program is added or removed in the collection <see cref="programsToClose"/> 
        /// </summary>
        public static event EventHandler<AutoCloseChangedArgs> AutoCloseProgramListChanged;
        internal static HashSet<ProgramToClose> programsToClose;

        public static void OnAutoCloseProgramListChanged(AutoCloseChangedArgs args)
        {
            AutoCloseProgramListChanged?.Invoke(programsToClose, args);
        }

        public class AutoCloseChangedArgs : EventArgs
        {
            public ProgramListAction ProgramAction { get; set; }
            public ProgramToClose programToClose;
        }

        public ProgramToClose(DateTime startDate, DateTime endDate, string programName, List<(TimeSpan startTime, TimeSpan endTime)> schedules, IDictionary<DayOfWeek, bool> noCloseDays) : base(startDate, endDate)
        {
            foreach ((TimeSpan startTime, TimeSpan endTime) schedule in schedules)
            {
                if (schedule.startTime > schedule.endTime)
                {
                    throw new InvalidOperationException($"We cannot have a start date {schedule.startTime} superior to an end date {schedule.endTime}");
                }
            }

            this.programName = programName.Split(".exe")[0] ?? throw new ArgumentNullException(nameof(programName));
            this.startDate = startDate;
            this.endDate = endDate;
            if (schedules != null)
            {
                this.schedules = schedules;
            }
            else
            {
                this.schedules = new List<(TimeSpan startTime, TimeSpan endTime)>();
            }
            this.noCloseDays = new Dictionary<DayOfWeek, bool> { };
            foreach (DayOfWeek dayOfWeek in Enum.GetValues(typeof(DayOfWeek)))
            {
                this.noCloseDays.Add(dayOfWeek, false || (noCloseDays != null ? noCloseDays.ContainsKey(dayOfWeek) == true : false));
            }
        }

        public override string ToString()
        {
            string schedules = Environment.NewLine;
            if (IsFullDayLock)
            {
                schedules = "Lock 24/7";
            }
            else
            {
                foreach ((TimeSpan startTime, TimeSpan endTime) schedule in this.schedules)
                {
                    schedules += $"      >> Start Time {schedule.startTime} | End Time {schedule.endTime}" + Environment.NewLine;
                }
            }
            return
            $"Program: {programName} " + Environment.NewLine +
            $"Locked: " + IsSecurityActive + Environment.NewLine + // TODO use a valid getter instead
            $"Never Lock Days : " + String.Join(',', noCloseDays
                    .Where((day) => day.Value == true)
                    .Select((day) => day.Key.ToString()))
                    + Environment.NewLine +
            $"Lock schedules: " + schedules + Environment.NewLine +
            $"Lock Start date: " + startDate.ToLongDateString() + " | " + startDate.ToLongTimeString() + Environment.NewLine +
            $"Unlock date: " + endDate.ToLongDateString() + " | " + endDate.ToLongTimeString() + Environment.NewLine +
            $"Time remaining: " + TimeRemaining;
        }

        public bool IsFullDayLock => schedules.Any((schedule) => schedule.startTime == schedule.endTime);

        private static void InitProcessWatcher()
        {
            if (watcher != null)
            {
                Console.WriteLine("Watcher already enabled");
                return;
            }
            processWatcherThread = new Thread(() =>
            {
                while (true)
                {

                    watcher = new ManagementEventWatcher
                    {
                        Query = new WqlEventQuery("__InstanceCreationEvent", new TimeSpan(0, 0, 1), "TargetInstance isa \"Win32_Process\""),
                    };
                    // times out watcher.WaitForNextEvent in 5 seconds
                    watcher.Options.Timeout = new TimeSpan(5, 0, 0);

                    ManagementBaseObject e = watcher.WaitForNextEvent();

                    string name = ((string)((ManagementBaseObject)e["TargetInstance"])["Name"]).Split(".exe")[0];
                    string path = ((string)((ManagementBaseObject)e["TargetInstance"])["ExecutablePath"]);
                    Console.WriteLine("Process {0} has been created, path is: {1}", name, path);
                    ProgramToClose program = programsToClose.FirstOrDefault(program => program.programName.ToLower().Contains(name.ToLower()));

                    // TODO : create getter, made of getters, to know when the program is simply locked and not just when the security  is within 2 dates
                    if (program != null)
                    {
                        Console.WriteLine("detected program " + program.programName);
                        DateTime now = DateTime.Now;
                        if (now > program.startDate && now < program.endDate)
                        {
                            Console.WriteLine("Still valid date for close");
                            foreach ((TimeSpan startTime, TimeSpan endTime) in program.schedules)
                            {
                                Console.WriteLine(startTime + "/" + endTime);

                                if ((startTime > now.TimeOfDay && now.TimeOfDay < endTime) || program.IsFullDayLock)
                                {
                                    NonAdmin.CloseProgram(name);
                                }
                            }
                        }
                    };
                    watcher.Stop();
                }
            });
            processWatcherThread.Start();

            Console.WriteLine($"Process Watcher for Auto Closing program is now enabled");
        }


        public static void DisposeProcessWatcher()
        {
            programsToClose?.Clear();
            watcher?.Stop();
            watcher?.Dispose();
            processWatcherThread?.Abort();
        }


        public override bool Equals(object obj)
        {
            return (obj as ProgramToClose).programName.Equals(programName);
        }

        public override int GetHashCode()
        {
            return programName.GetHashCode();
        }

        protected override void _PrepareEnableSecurity()
        {
            if (processWatcherThread == null)
            {
                InitProcessWatcher();
            }
        }

        protected override void _EnableSecurity()
        {
            if (programsToClose == null || programsToClose.Count == 0)
            {
                programsToClose = new HashSet<ProgramToClose> { };
                InitProcessWatcher();
            };
            programsToClose.Add(this);
        }

        protected override void _DisableSecurity()
        {
            programsToClose.Remove(this);
            if (programsToClose.Count == 0 && processWatcherThread != null)
            {
                DisposeProcessWatcher();
            }
        }
    }
}
