using System;
using System.IO;
using System.Reflection;

namespace Bolter
{
    public class Globals
    {
        public const string ENVIRONMENT_VARIABLE_MOTIVATOR_FOLDER_PATH = "MotivatorFolderPath";
        public static string MOTIVATOR_FOLDER_PATH = Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE_MOTIVATOR_FOLDER_PATH);
        public static string AdminAppParentFolder
        {
            get
            {
                string exePathOfMotivator = Assembly.GetEntryAssembly().Location;
                string parentPathOfMotivator = Path.GetDirectoryName(exePathOfMotivator);
                return @$"{parentPathOfMotivator}";
            }
        }

        public static void CheckDependencies()
        {
            if (MOTIVATOR_FOLDER_PATH == null)
            {
                throw new ArgumentNullException($"Windows Environment variable missing : {Globals.ENVIRONMENT_VARIABLE_MOTIVATOR_FOLDER_PATH}");
            }
            else if (!Directory.Exists(MOTIVATOR_FOLDER_PATH))
            {
                throw new ArgumentNullException("The path indicated is invalid for the main motivator folder : " + MOTIVATOR_FOLDER_PATH);
            }
        }


    }
}
