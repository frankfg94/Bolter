using System;
using System.Management;
using System.Text;

namespace Bolter.Program
{
    public class ProcessWatcher
    {
        private ManagementEventWatcher watcher;
        public event EventHandler<ProcessCreatedEventArgs> ProcessCreated;

        public ProcessWatcher()
        {
            watcher = new ManagementEventWatcher(new WqlEventQuery("__InstanceCreationEvent", new TimeSpan(0, 0, 1), "TargetInstance isa \"Win32_Process\""));
            watcher.EventArrived += OnProcessCreated;
            watcher.Start();
        }

        private void OnProcessCreated(object sender, EventArrivedEventArgs e)
        {
            string name = ((string)((ManagementBaseObject)e.NewEvent["TargetInstance"])["Name"]).Split(".exe")[0];
            string path = (string)((ManagementBaseObject)e.NewEvent["TargetInstance"])["ExecutablePath"];
            Console.WriteLine("Process {0} has been created, path is: {1}", name, path);


            // Créez l'objet d'arguments
            ProcessCreatedEventArgs eventArgs = new ProcessCreatedEventArgs(name, path);

            // Déclenchez l'événement ProcessCreated
            ProcessCreated?.Invoke(this, eventArgs);
        }

        public void Stop()
        {
            watcher.EventArrived -= OnProcessCreated;
            watcher.Stop();
            watcher.Dispose();
        }
    }
}
