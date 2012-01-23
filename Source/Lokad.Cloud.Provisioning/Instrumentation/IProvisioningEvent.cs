#region Copyright (c) Lokad 2010-2012
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System.Xml.Linq;

namespace Lokad.Cloud.Provisioning.Instrumentation
{
    /// <summary>
    /// Provisioning System Event (Instrumentation)
    /// </summary>
    public interface IProvisioningEvent
    {
        ProvisioningEventLevel Level { get; }
        string Describe();
        XElement DescribeMeta();
    }
}
