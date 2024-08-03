using System;
using System.IO;
using System.Reflection;

namespace Bolter
{
    public static class Globals
    {
     
        public static string AdminAppParentFolder
        {
            get
            {
                string exePathOfAppRunningBolter = Assembly.GetEntryAssembly().Location;
                return Path.GetDirectoryName(exePathOfAppRunningBolter);
            }
        }


    }
}
