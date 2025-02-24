using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Occurrence
{
    public class ActionProperties
    {
        public Guid Id { get; set; }
        public Guid SdkMessageId { get; set; }
        public int Category { get; set; }
        public string Name { get; set; }
        public string UniqueName { get; set; }
        public string Xaml { get; set; }
        public int StatusCode { get; set; }
        public string PrimaryEntity { get; set; }
    }

    public class ActionParameters
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool Required { get; set; }
        public bool IsTarget { get; set; }
        public string EntityRef { get; set; }
        public string Direction { get; set; }
    }

    public class OccurrenceType
    {
        public EntityReference entity { get; set; }
        public string typeId { get; set; }
        public Guid triggerAction { get; set; }
        public Guid completionAction { get; set; }
        public bool enableOnDeactiveMode { get; set; }
        public bool enableTarget{ get; set; }
        public string targetEntity { get; set; }
        public string targetfield { get; set; }
        public int stateCode { get; set; }
    }
}
