#region Copyright (c) Lokad 2011
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Net;

namespace Lokad.Cloud.Provisioning.Instrumentation.Events
{
    public class ProvisioningFailedPermanentEvent : ICloudProvisioningEvent
    {
        public AggregateException Exception { get; private set; }
        public HttpStatusCode HttpStatus { get; private set; }

        public ProvisioningFailedPermanentEvent(AggregateException exception, HttpStatusCode httpStatus = HttpStatusCode.Unused)
        {
            Exception = exception;
            HttpStatus = httpStatus;
        }
    }
}
