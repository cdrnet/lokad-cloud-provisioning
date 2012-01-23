#region Copyright (c) Lokad 2010-2012
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Xml.Linq;

namespace Lokad.Cloud.Provisioning.Instrumentation.Events
{
    public class ProvisioningFailedBecauseOfConflictEvent : IProvisioningEvent
    {
        public ProvisioningEventLevel Level { get { return ProvisioningEventLevel.Trace; } }
        public AggregateException Exception { get; private set; }

        public ProvisioningFailedBecauseOfConflictEvent(AggregateException exception)
        {
            Exception = exception;
        }

        public string Describe()
        {
            return string.Format("Provisioning failed temporarily because of a conflict: {0}",
                Exception != null ? Exception.Message : string.Empty);
        }

        public XElement DescribeMeta()
        {
            return DescribeEvent.Meta(Exception, "ProvisioningFailedBecauseOfConflictEvent");
        }
    }
}
