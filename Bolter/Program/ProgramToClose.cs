using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Xml.Linq;
using static Bolter.NonAdmin;

namespace Bolter.Program
{
    public class ProgramToClose : AbstractSecurity
    {
        public readonly string programName;

        public List<(TimeSpan startTime, TimeSpan endTime)> schedules;
        /// <summary>
        /// Indicates day of the weeks where this program won't be automatically closed
        /// </summary>
        public readonly Dictionary<DayOfWeek, bool> noCloseDays;
        private static ProcessWatcher processWatcher;

        // TODO : see to use cancellation Token instead of thread abort and see if relevant

        /// <summary>
        /// Triggered when a program is added or removed in the collection <see cref="programsToClose"/> 
        /// </summary>
        public static event EventHandler<AutoCloseChangedArgs> AutoCloseProgramListChanged;
        internal static HashSet<ProgramToClose> programsToClose;
        private readonly List<string> linkedPrograms = new List<string>();

        public static void OnAutoCloseProgramListChanged(AutoCloseChangedArgs args)
        {
            AutoCloseProgramListChanged?.Invoke(programsToClose, args);
        }

        public class AutoCloseChangedArgs : EventArgs
        {
            public ProgramListAction ProgramAction { get; set; }
            public ProgramToClose programToClose;
        }

        public ProgramToClose(DateTime startDate, DateTime endDate, string programName, List<(TimeSpan startTime, TimeSpan endTime)> schedules, IDictionary<DayOfWeek, bool> noCloseDays, List<string> linkedPrograms = null) : base(startDate, endDate)
        {
            if (string.IsNullOrEmpty(programName))
            {
                throw new ArgumentException($"« {nameof(programName)} » ne peut pas être vide ou avoir la valeur Null.", nameof(programName));
            }
            if (schedules == null) schedules = new List<(TimeSpan startTime, TimeSpan endTime)>();

            foreach ((TimeSpan startTime, TimeSpan endTime) in schedules)
            {
                if (startTime > endTime)
                {
                    throw new InvalidOperationException($"We cannot have a start date {startTime} superior to an end date {endTime}");
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

            this.linkedPrograms = linkedPrograms;
        }

        public bool IsDateValid
        {
            get
            {
                DateTime now = DateTime.Now;
                return now > startDate && now < endDate;
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
            $"Never Lock Days : " + string.Join(',', noCloseDays
                    .Where((day) => day.Value == true)
                    .Select((day) => day.Key.ToString()))
                    + Environment.NewLine +
            $"Lock schedules: " + schedules + Environment.NewLine +
            $"Lock Start date: " + startDate.ToLongDateString() + " | " + startDate.ToLongTimeString() + Environment.NewLine +
            $"Unlock date: " + endDate.ToLongDateString() + " | " + endDate.ToLongTimeString() + Environment.NewLine +
            $"Time remaining: " + TimeRemaining;
        }

        public bool IsFullDayLock => schedules.Count == 0 || schedules.Any((schedule) => schedule.startTime == schedule.endTime);
        public bool IsCurrentScheduleLocked
        {
            get
            {
                if (IsFullDayLock) return true;
                foreach ((TimeSpan startTime, TimeSpan endTime) in schedules)
                {
                    Console.WriteLine(startTime + "/" + endTime);
                    DateTime now = DateTime.Now;
                    if (startTime > now.TimeOfDay && now.TimeOfDay < endTime)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private static void CloseProgramIfFordbidden(string name)
        {
            ProgramToClose program = programsToClose.FirstOrDefault(program => program.programName.ToLower().Contains(name.ToLower()));
            if (program != null)
            {
                Console.WriteLine("detected program start " + program.programName);
                DateTime now = DateTime.Now;
                if (program.IsDateValid)
                {
                    Console.WriteLine("Still valid date for close");
                    if (program.IsCurrentScheduleLocked)
                    {
                        CloseProgram(program.programName);
                        foreach (var linkedProgram in program.linkedPrograms)
                        {
                            CloseProgram(linkedProgram);
                        }
                    }
                }
            };
        }

        private static void InitProcessWatcher()
        {
            if (processWatcher != null)
            {
                Console.WriteLine("Process watcher already enabled");
                return;
            }
            processWatcher = new ProcessWatcher();
            processWatcher.ProcessCreated += (sender, programEvent) =>
            {
                CloseProgramIfFordbidden(programEvent.Name);
            };
            Console.WriteLine($"Process Watcher for Auto Closing program is now enabled");

        }


        public static void DisposeProcessWatcher()
        {
            programsToClose?.Clear();
            processWatcher?.Stop();
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
            if (processWatcher == null)
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
            CloseProgramIfFordbidden(programName);
        }

        protected override void _DisableSecurity()
        {
            programsToClose.Remove(this);
            if (programsToClose.Count == 0 && processWatcher != null)
            {
                DisposeProcessWatcher();
            }
        }
    }
}
