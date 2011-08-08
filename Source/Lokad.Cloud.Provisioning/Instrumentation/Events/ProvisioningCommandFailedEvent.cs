#region Copyright (c) Lokad 2011
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;

namespace Lokad.Cloud.Provisioning.Instrumentation.Events
{
    public class ProvisioningCommandFailedEvent : ICloudProvisioningEvent
    {
        public AggregateException Exception { get; private set; }

        public ProvisioningCommandFailedEvent(AggregateException exception)
        {
            Exception = exception;
        }
    }
}
