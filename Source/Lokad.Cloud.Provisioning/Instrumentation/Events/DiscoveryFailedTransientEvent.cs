#region Copyright (c) Lokad 2010-2012
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Net;
using System.Xml.Linq;

namespace Lokad.Cloud.Provisioning.Instrumentation.Events
{
    public class DiscoveryFailedTransientEvent : IProvisioningEvent
    {
        public ProvisioningEventLevel Level { get { return ProvisioningEventLevel.Trace; } }
        public AggregateException Exception { get; private set; }
        public HttpStatusCode HttpStatus { get; private set; }

        public DiscoveryFailedTransientEvent(AggregateException exception, HttpStatusCode httpStatus = HttpStatusCode.Unused)
        {
            Exception = exception;
            HttpStatus = httpStatus;
        }

        public string Describe()
        {
            return string.Format("Provisioning Discovery failed temporarily with HTTP Status {0}: {1}",
                HttpStatus, Exception != null ? Exception.Message : string.Empty);
        }

        public XElement DescribeMeta()
        {
            return DescribeEvent.Meta(Exception, "DiscoveryFailedTransientEvent");
        }
    }
}
