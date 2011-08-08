#region Copyright (c) Lokad 2011
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Net;

namespace Lokad.Cloud.Provisioning.Instrumentation.Events
{
    public class ProvisioningCommandFailedTransientEvent : ICloudProvisioningEvent
    {
        public AggregateException Exception { get; private set; }
        public HttpStatusCode HttpStatus { get; private set; }

        public ProvisioningCommandFailedTransientEvent(AggregateException exception, HttpStatusCode httpStatus = HttpStatusCode.Unused)
        {
            Exception = exception;
            HttpStatus = httpStatus;
        }
    }
}
