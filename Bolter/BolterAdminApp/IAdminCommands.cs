using System;
using System.Collections.Generic;
using System.Text;

namespace Bolter.BolterAdminApp
{
    /// <summary>
    /// Disable all possible restrictions is not present because all non admin features can be disabled on the client side, also the variables must be cleared client side
    /// </summary>
    public interface IAdminCommands
    {
        void RequestInstallNtRights();
        void RequestSetBatchAndCMDBlock(bool block);
        void RequestPreventDateEditingW10(bool removePrivilege);
        void RequestUninstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service");
        void RequestInstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service", bool autoStart = true);
        void RequestHideStartupsAppsFromSettings(bool hide);
        void RequestDisableAllAdminRestrictions(string appPath);
        void RequestDisableAllAdminRestrictions(string appPath, string[] foldersPathToUnlock);
        void RequestSetWebsiteBlocked(bool block, string domain);
        void RequestSetStartupSafeMode(bool autoStartEnabled, string applicationFullPath = "useThisApp");
    }
}
