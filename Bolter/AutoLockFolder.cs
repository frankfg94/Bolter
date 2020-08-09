using System;
using System.Collections.Generic;
using System.Text;

namespace Bolter
{
    public class AutoLockFolder
    {
        public string path;
        public DateTime startDate;
        public DateTime endDate;

        public AutoLockFolder(string programName, DateTime startTime, DateTime endTime)
        {
            this.path = programName;
            this.startDate = startTime;
            this.endDate = endTime;
        }

        public override bool Equals(object obj)
        {
            AutoLockFolder folder = obj as AutoLockFolder;
            return obj!=null && folder.path.Equals(path, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return path.GetHashCode();
        }
    }
}
