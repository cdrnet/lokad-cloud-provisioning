#region Copyright (c) Lokad 2010-2012
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Xml.Linq;

namespace Lokad.Cloud.Provisioning.Instrumentation.Events
{
    /// <summary>
    /// Raised whenever a provisioning operation is retried.
    /// Useful for analyzing retry policy behavior.
    /// </summary>
    public class ProvisioningOperationRetriedEvent : IProvisioningEvent
    {
        public ProvisioningEventLevel Level { get { return ProvisioningEventLevel.Trace; } }
        public Exception Exception { get; private set; }
        public string Policy { get; private set; }
        public int Trial { get; private set; }
        public TimeSpan Interval { get; private set; }
        public Guid TrialSequence { get; private set; }

        public ProvisioningOperationRetriedEvent(Exception exception, string policy, int trial, TimeSpan interval, Guid trialSequence)
        {
            Exception = exception;
            Policy = policy;
            Trial = trial;
            Interval = interval;
            TrialSequence = trialSequence;
        }

        public string Describe()
        {
            return string.Format("Provisioning operation was retried on policy {0} ({1} trial): {2}",
                Policy, Trial,
                Exception != null ? Exception.Message : string.Empty);
        }

        public XElement DescribeMeta()
        {
            var meta = new XElement("Meta");

            if (Exception != null)
            {
                meta.Add(new XElement("Exception",
                    new XAttribute("typeName", Exception.GetType().FullName),
                    new XAttribute("message", Exception.Message),
                    Exception.ToString()));
            }

            return meta;
        }
    }
}
