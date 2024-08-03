using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Bolter
{
    // The big boss
    public class BolterCEO
    {
        public static void Configure(string searchFolderForInstallation)
        {
            if (string.IsNullOrEmpty(searchFolderForInstallation))
            {
                throw new ArgumentNullException(nameof(searchFolderForInstallation), "Search folder must not be null or empty");
            }

            if (!Directory.Exists(searchFolderForInstallation))
            {
                throw new DirectoryNotFoundException($"The folder indicated '{searchFolderForInstallation}' doesn't exist.");
            }

            Admin.SearchFolderForInstallation = searchFolderForInstallation;
        }

        public static bool IsConfigured()
        {
            return Admin.SearchFolderForInstallation != null;
        }
    }
}
