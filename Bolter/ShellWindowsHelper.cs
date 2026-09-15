using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Bolter
{
    /// <summary>
    /// Accesses the Windows Shell window collection without a generated SHDocVw interop assembly.
    /// </summary>
    public static class ShellWindowsHelper
    {
        public static void CloseAll()
        {
            ForEachWindow(window => Invoke(window, "Quit"));
        }

        public static void CloseSpecific(string specificWindowUrl)
        {
            ForEachWindow(window =>
            {
                var locationUrl = GetProperty(window, "LocationURL") as string;
                if (string.Equals(locationUrl, specificWindowUrl, StringComparison.OrdinalIgnoreCase))
                {
                    Invoke(window, "Quit");
                }
            });
        }

        public static string[] GetPaths()
        {
            var paths = new List<string>();
            ForEachWindow(window =>
            {
                var locationUrl = GetProperty(window, "LocationURL") as string;
                if (locationUrl != null)
                {
                    paths.Add(locationUrl);
                }
            });
            return paths.ToArray();
        }

        private static void ForEachWindow(Action<object> action)
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
            {
                return;
            }

            object shellApplication = Activator.CreateInstance(shellType);
            object shellWindows = null;
            try
            {
                shellWindows = Invoke(shellApplication, "Windows");
                var count = Convert.ToInt32(GetProperty(shellWindows, "Count"), CultureInfo.InvariantCulture);
                for (var index = 0; index < count; index++)
                {
                    object window = null;
                    try
                    {
                        window = Invoke(shellWindows, "Item", index);
                        if (window != null)
                        {
                            action(window);
                        }
                    }
                    finally
                    {
                        ReleaseComObject(window);
                    }
                }
            }
            finally
            {
                ReleaseComObject(shellWindows);
                ReleaseComObject(shellApplication);
            }
        }

        private static object GetProperty(object target, string name)
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty,
                binder: null,
                target,
                args: null);
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.GetProperty,
                binder: null,
                target,
                args);
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
    }
}
