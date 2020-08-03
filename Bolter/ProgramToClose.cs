using System;
using System.Collections.Generic;
using System.Text;

namespace Bolter
{
    public class ProgramToClose
    {
        public string programName;
        public TimeSpan startTime;
        public TimeSpan endTime;

        public ProgramToClose(string programName, TimeSpan startTime, TimeSpan endTime)
        {
            this.programName = programName ?? throw new ArgumentNullException(nameof(programName));
            this.startTime = startTime;
            this.endTime = endTime;
        }


        public override bool Equals(object obj)
        {
            return (obj as ProgramToClose).programName.Equals(programName);
        }

        public override int GetHashCode()
        {
            return programName.GetHashCode();
        }
    }
}
