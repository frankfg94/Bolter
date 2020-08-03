using System;
using System.Collections.Generic;
using System.Text;

namespace Bolter
{
    public class FolderToLock
    {
        public string path;
        public DateTime startDate;
        public DateTime endDate;

        public FolderToLock(string programName, DateTime startTime, DateTime endTime)
        {
            this.path = programName;
            this.startDate = startTime;
            this.endDate = endTime;
        }

        public override bool Equals(object obj)
        {
            return (obj as FolderToLock).path.Equals(path);
        }

        public override int GetHashCode()
        {
            return path.GetHashCode();
        }
    }
}
