﻿#region Copyright (c) Lokad 2010-2011
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Cloud.Provisioning.Info;
using Lokad.Cloud.Provisioning.Instrumentation;
using Lokad.Cloud.Provisioning.Instrumentation.Events;
using Lokad.Cloud.Provisioning.Internal;

namespace Lokad.Cloud.Provisioning
{
    public class AzureDiscovery
    {
        readonly string _subscriptionId;
        readonly X509Certificate2 _certificate;
        readonly ICloudProvisioningObserver _observer;
        readonly RetryPolicies _policies;

        public AzureDiscovery(string subscriptionId, X509Certificate2 certificate, ICloudProvisioningObserver observer = null)
        {
            _subscriptionId = subscriptionId;
            _certificate = certificate;
            _observer = observer;
            _policies = new RetryPolicies(observer);
        }

        public Task<HostedServiceInfo> DiscoverHostedService(string serviceName, CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var task = DoDiscoverHostedService(client, serviceName, cancellationToken);
            task.ContinueRaiseSystemEventOnFault(_observer, EventForFailedOperation);
            return task;
        }

        public Task<HostedServiceInfo[]> DiscoverHostedServices(CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var task = DoDiscoverHostedServices(client, cancellationToken);
            task.ContinueRaiseSystemEventOnFault(_observer, EventForFailedOperation);
            return task;
        }

        public Task<DeploymentReference> DiscoverDeployment(string deploymentPrivateId, CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var completionSource = new TaskCompletionSource<DeploymentReference>();
            DoDiscoverDeploymentAsync(client, deploymentPrivateId, completionSource, cancellationToken);
            completionSource.Task.ContinueRaiseSystemEventOnFault(_observer, EventForFailedOperation);
            return completionSource.Task;
        }

        internal static ICloudProvisioningEvent EventForFailedOperation(AggregateException exception)
        {
            HttpStatusCode httpStatus;
            if (ProvisioningErrorHandling.TryGetHttpStatusCode(exception, out httpStatus))
            {
                return ProvisioningErrorHandling.IsTransientError(exception)
                    ? (ICloudProvisioningEvent)new DiscoveryFailedTransientEvent(exception, httpStatus)
                    : new DiscoveryFailedPermanentEvent(exception, httpStatus);
            }

            return ProvisioningErrorHandling.IsTransientError(exception)
                ? (ICloudProvisioningEvent)new DiscoveryFailedTransientEvent(exception)
                : new DiscoveryFailedPermanentEvent(exception);
        }

        internal void DoDiscoverDeploymentAsync(HttpClient client, string deploymentPrivateId, TaskCompletionSource<DeploymentReference> completionSource, CancellationToken cancellationToken)
        {
            DoDiscoverDeployments(client, cancellationToken).ContinuePropagateWith(completionSource, cancellationToken, task =>
                {
                    var deployment = task.Result.FirstOrDefault(d => d.DeploymentPrivateId == deploymentPrivateId);
                    if (deployment != null)
                    {
                        completionSource.TrySetResult(deployment);
                    }
                    else
                    {
                        completionSource.TrySetException(new KeyNotFoundException());
                    }
                });
        }

        Task<DeploymentReference[]> DoDiscoverDeployment(HttpClient client, string serviceName, CancellationToken cancellationToken)
        {
            return client.GetXmlAsync<DeploymentReference[]>(
                string.Format("services/hostedservices/{0}?embed-detail=true", serviceName),
                cancellationToken, _policies.RetryOnTransientErrors,
                (xml, tcs) =>
                {
                    var xmlService = xml.AzureElement("HostedService");
                    var references = xmlService.AzureElements("Deployments", "Deployment").Select(d => new DeploymentReference
                    {
                        HostedServiceName = xmlService.AzureValue("ServiceName"),
                        DeploymentName = d.AzureValue("Name"),
                        DeploymentPrivateId = d.AzureValue("PrivateID")
                    }).ToArray();

                    tcs.TrySetResult(references);
                });
        }

        Task<DeploymentReference[]> DoDiscoverDeployments(HttpClient client, CancellationToken cancellationToken)
        {
            return client.GetXmlAsync<DeploymentReference[]>(
                "services/hostedservices",
                cancellationToken, _policies.RetryOnTransientErrors,
                (xml, tcs) =>
                {
                    var serviceNames = xml.AzureElements("HostedServices", "HostedService")
                        .Select(e => e.AzureValue("ServiceName"))
                        .ToArray();

                    Task.Factory.ContinueWhenAll(
                        serviceNames.Select(serviceName => DoDiscoverDeployment(client, serviceName, cancellationToken)).ToArray(),
                        tasks =>
                        {
                            // TODO (ruegg, 2011-05-27): Check task fault state and deal with it

                            try
                            {
                                tcs.TrySetResult(tasks.SelectMany(t => t.Result).ToArray());
                            }
                            catch (Exception e)
                            {
                                tcs.TrySetException(e);
                            }
                        },
                        cancellationToken);
                });
        }

        Task<HostedServiceInfo> DoDiscoverHostedService(HttpClient client, string serviceName, CancellationToken cancellationToken)
        {
            return client.GetXmlAsync<HostedServiceInfo>(
                string.Format("services/hostedservices/{0}?embed-detail=true", serviceName),
                cancellationToken, _policies.RetryOnTransientErrors,
                (xml, tcs) =>
                {
                    var xmlService = xml.AzureElement("HostedService");
                    var xmlProperties = xmlService.AzureElement("HostedServiceProperties");

                    var info = new HostedServiceInfo
                    {
                        ServiceName = xmlService.AzureValue("ServiceName"),
                        Description = xmlProperties.AzureValue("Description"),
                        ServiceLabel = xmlProperties.AzureEncodedValue("Label"),

                        Deployments = xmlService.AzureElements("Deployments", "Deployment").Select(d =>
                        {
                            var config = d.AzureConfiguration();
                            var instanceCountPerRole = d.AzureElements("RoleInstanceList", "RoleInstance")
                                .GroupBy(ri => ri.AzureValue("RoleName"))
                                .ToDictionary(g => g.Key, g => g.Count());

                            return new DeploymentInfo
                            {
                                DeploymentName = d.AzureValue("Name"),
                                DeploymentLabel = d.AzureEncodedValue("Label"),
                                Slot = (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), d.AzureValue("DeploymentSlot")),
                                PrivateId = d.AzureValue("PrivateID"),
                                Status = (DeploymentStatus)Enum.Parse(typeof(DeploymentStatus), d.AzureValue("Status")),
                                Roles = d.AzureElements("RoleList", "Role").Select(r =>
                                {
                                    var roleName = r.AzureValue("RoleName");
                                    var roleConfig = config.ServiceConfigElements("ServiceConfiguration", "Role")
                                        .Single(role => role.AttributeValue("name") == roleName);
                                    
                                    int instanceCount;
                                    if (!instanceCountPerRole.TryGetValue(roleName, out instanceCount))
                                    {
                                        instanceCount = 0;
                                    }

                                    return new RoleInfo
                                    {
                                        RoleName = roleName,
                                        ActualInstanceCount = instanceCount,
                                        ConfiguredInstanceCount = Int32.Parse(roleConfig.ServiceConfigElement("Instances").AttributeValue("count")),
                                        Settings = roleConfig.ServiceConfigElements("ConfigurationSettings", "Setting").ToDictionary(
                                            x => x.AttributeValue("name"), x => x.AttributeValue("value"))
                                    };
                                }).ToList()
                            };
                        }).ToList()
                    };

                    tcs.TrySetResult(info);
                });
        }

        Task<HostedServiceInfo[]> DoDiscoverHostedServices(HttpClient client, CancellationToken cancellationToken)
        {
            return client.GetXmlAsync<HostedServiceInfo[]>(
                "services/hostedservices",
                cancellationToken, _policies.RetryOnTransientErrors,
                (xml, tcs) =>
                {
                    var serviceNames = xml.AzureElements("HostedServices", "HostedService")
                        .Select(e => e.AzureValue("ServiceName"))
                        .ToArray();

                    Task.Factory.ContinueWhenAll(
                        serviceNames.Select(serviceName => DoDiscoverHostedService(client, serviceName, cancellationToken)).ToArray(),
                        tasks =>
                        {
                            // TODO (ruegg, 2011-05-27): Check task fault state and deal with it

                            try
                            {
                                tcs.TrySetResult(tasks.Select(t => t.Result).ToArray());
                            }
                            catch (Exception e)
                            {
                                tcs.TrySetException(e);
                            }
                        },
                        cancellationToken);
                });
        }
    }
}
