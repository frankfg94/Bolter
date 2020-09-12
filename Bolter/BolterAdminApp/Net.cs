using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Bolter.BolterAdminApp
{
    public class Net
    {
        public static void SendMsg(Stream s, string msg)
        {
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(s, msg);
        }

        public static string RcvMsg(Stream s)
        {
            lock (s)
            {
                BinaryFormatter bf = new BinaryFormatter();
                return (string)bf.Deserialize(s);
            }
        }

    }
}
