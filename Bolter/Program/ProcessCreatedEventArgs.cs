using System;
using System.Collections.Generic;
using System.Text;

namespace Bolter.Program
{
    public class ProcessCreatedEventArgs : EventArgs
    {
        public string Name { get; }
        public string Path { get; }
        public ProcessCreatedEventArgs(string name, string path)
        {
            Name = name;
            Path = path;
        }
    }
}
