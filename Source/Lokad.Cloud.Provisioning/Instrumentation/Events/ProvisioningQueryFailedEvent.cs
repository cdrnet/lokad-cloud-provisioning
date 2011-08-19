﻿#region Copyright (c) Lokad 2011
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;

namespace Lokad.Cloud.Provisioning.Instrumentation.Events
{
    public class ProvisioningQueryFailedEvent : ICloudProvisioningEvent
    {
        public AggregateException Exception { get; private set; }

        public ProvisioningQueryFailedEvent(AggregateException exception)
        {
            Exception = exception;
        }
    }
}
