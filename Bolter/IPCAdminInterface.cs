using System;
using System.Collections.Generic;
using System.Text;

namespace Bolter
{
    interface IPCAdminInterface
    {
        void RequestInstallNtRights();
        void RequestSetBatchAndCMDBlock(bool block);
        void RequestPreventDateEditingW10(bool removePrivilege);
        void RequestUninstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service");
        void RequestInstallService(string serviceExeName = "AdminBolterService", string serviceName = "Bolter Admin Service", bool autoStart = true);
        void RequestHideStartupsAppsFromSettings(bool hide);
        void RequestDisableAllAdminRestrictions(string appPath);
        void RequestDisableAllAdminRestrictions(string appPath, string[] foldersPathToUnlock);
        void RequestDisableAllPossibleRestrictions(string appPath);
        void RequestSetWebsiteBlocked(bool block, string domain);
        void RequestSetStartupSafeMode(bool autoStartEnabled, string applicationFullPath = "useThisApp");
    }
}
