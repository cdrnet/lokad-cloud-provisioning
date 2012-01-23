#region Copyright (c) Lokad 2010-2012
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Xml.Linq;

namespace Lokad.Cloud.Provisioning.Instrumentation
{
    internal static class DescribeEvent
    {
        internal static XElement Meta(AggregateException exception, string eventName)
        {
            var meta = new XElement("Meta",
                new XElement("Component", "Lokad.Cloud.Provisioning"),
                new XElement("Event", eventName));

            if (exception != null)
            {
                var ex = exception.GetBaseException();
                meta.Add(new XElement("Exception",
                    new XAttribute("typeName", ex.GetType().FullName),
                    new XAttribute("message", ex.Message),
                    ex.ToString()));
            }

            return meta;
        }
    }
}
