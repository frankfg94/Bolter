using System;
using System.Collections.Generic;
using System.Text;

namespace Bolter.Attributes
{
    /// <summary>
    /// This function requires the calling app to be in administrator, try the bridge or the service if you don't want the main app to be in administrator mode
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.Method)]
    public class RequireAdministrator : Attribute
    {
        public const string _message = "This function requires the app to be in administrator, try the bridge or the service if you don't want the main app to be in administrator mode";
        public RequireAdministrator()
        {
            if(!Bolter.NonAdmin.IsInAdministratorMode())
            {
                throw new UnauthorizedAccessException(_message);
            }
        }

        public String Message
        {
            get { return _message; }
        }
    }
}
