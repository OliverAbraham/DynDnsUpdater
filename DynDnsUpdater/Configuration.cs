using System.Collections.Generic;

namespace DynDnsUpdater
{
    class Configuration
    {
        public string                       LocalIPV6Address              { get; set; }
        public int                          TimeoutSeconds                { get; set; }
        public int                          WaitTimeSeconds               { get; set; }
        public bool                         LogUnchangedAddress           { get; set; }
        public List<Source>                 Sources                       { get; set; }
        public List<Target>                 Targets                       { get; set; }
        public Notifications                Notifications                 { get; set; }
        public InterProcessCommunication    InterProcessCommunication     { get; set; }

        public Configuration()
        {
            Sources = new List<Source>();
            Targets = new List<Target>();
            Notifications = new Notifications();
            InterProcessCommunication = new InterProcessCommunication();
        }
    }
}
