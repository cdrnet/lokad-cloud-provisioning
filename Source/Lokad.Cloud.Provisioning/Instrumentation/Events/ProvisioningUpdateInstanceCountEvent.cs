#region Copyright (c) Lokad 2010-2012
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System.Xml.Linq;

namespace Lokad.Cloud.Provisioning.Instrumentation.Events
{
    /// <summary>
    /// Raised whenever a provisioning operation updates the instance count.
    /// Useful for analyzing instance count over time.
    /// </summary>
    public class ProvisioningUpdateInstanceCountEvent : IProvisioningEvent
    {
        public ProvisioningEventLevel Level { get { return ProvisioningEventLevel.Information; } }
        public int CurrentInstanceCount { get; private set; }
        public int RequestedInstanceCount { get; private set; }

        public ProvisioningUpdateInstanceCountEvent(int currentInstanceCount, int requestedInstanceCount)
        {
            CurrentInstanceCount = currentInstanceCount;
            RequestedInstanceCount = requestedInstanceCount;
        }

        public string Describe()
        {
            return string.Format("Provisioning requested {0} instances, from currently {1}.",
                RequestedInstanceCount, CurrentInstanceCount);
        }

        public XElement DescribeMeta()
        {
            return new XElement("Meta",
                new XElement("Component", "Lokad.Cloud.Provisioning"),
                new XElement("Event", "ProvisioningUpdateInstanceCountEvent"),
                new XElement("CurrentInstanceCount", CurrentInstanceCount),
                new XElement("RequestedInstanceCount", RequestedInstanceCount));
        }
    }
}
