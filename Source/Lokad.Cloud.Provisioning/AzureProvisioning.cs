﻿#region Copyright (c) Lokad 2010-2011
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Lokad.Cloud.Provisioning.Info;
using Lokad.Cloud.Provisioning.Instrumentation;
using Lokad.Cloud.Provisioning.Instrumentation.Events;
using Lokad.Cloud.Provisioning.Internal;

namespace Lokad.Cloud.Provisioning
{
    public class AzureProvisioning
    {
        readonly string _subscriptionId;
        readonly X509Certificate2 _certificate;
        readonly ICloudProvisioningObserver _observer;
        readonly RetryPolicies _policies;

        public AzureProvisioning(string subscriptionId, X509Certificate2 certificate, ICloudProvisioningObserver observer = null)
        {
            _subscriptionId = subscriptionId;
            _certificate = certificate;
            _observer = observer;
            _policies = new RetryPolicies(observer);
        }

        public Task<int> GetRoleInstanceCount(string serviceName, string roleName, DeploymentSlot deploymentSlot, CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var completionSource = new TaskCompletionSource<int>();

            DoGetDeploymentConfiguration(client, serviceName, deploymentSlot, cancellationToken).ContinuePropagateWith(
                completionSource, cancellationToken,
                queryTask => completionSource.TrySetResult(Int32.Parse(GetInstanceCountConfigElement(queryTask.Result, roleName).Value)));

            completionSource.Task.ContinueRaiseSystemEventOnFault(_observer, EventForFailedOperation);

            return completionSource.Task;
        }

        public Task<int> GetRoleInstanceCount(string serviceName, string roleName, string deploymentName, CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var completionSource = new TaskCompletionSource<int>();

            DoGetDeploymentConfiguration(client, serviceName, deploymentName, cancellationToken).ContinuePropagateWith(
                completionSource, cancellationToken,
                queryTask => completionSource.TrySetResult(Int32.Parse(GetInstanceCountConfigElement(queryTask.Result, roleName).Value)));

            completionSource.Task.ContinueRaiseSystemEventOnFault(_observer, EventForFailedOperation);

            return completionSource.Task;
        }

        public Task<int> GetCurrentInstanceCount(AzureCurrentDeployment currentDeployment, string roleName, CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var completionSource = new TaskCompletionSource<int>();

            currentDeployment.Discover(cancellationToken).ContinuePropagateWith(
                completionSource, cancellationToken,
                discoveryTask => DoGetDeploymentConfiguration(client, discoveryTask.Result.HostedServiceName, discoveryTask.Result.DeploymentName, cancellationToken).ContinuePropagateWith(
                    completionSource, cancellationToken,
                    queryTask => completionSource.TrySetResult(Int32.Parse(GetInstanceCountConfigElement(queryTask.Result, roleName).Value))));

            completionSource.Task.ContinueRaiseSystemEventOnFault(_observer, EventForFailedOperation);

            return completionSource.Task;
        }

        public Task UpdateRoleInstanceCount(string serviceName, string roleName, DeploymentSlot deploymentSlot, int instanceCount, CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var completionSource = new TaskCompletionSource<HttpStatusCode>();

            DoGetDeploymentConfiguration(client, serviceName, deploymentSlot, cancellationToken)
                .ContinuePropagateWith(completionSource, cancellationToken, queryTask =>
                    {
                        var config = queryTask.Result;
                        if (!UpdateInstanceCountConfig(config, roleName, instanceCount))
                        {
                            completionSource.TrySetResult(HttpStatusCode.NotModified);
                            return;
                        }

                        DoUpdateDeploymentConfiguration(client, serviceName, deploymentSlot, config, cancellationToken)
                            .ContinuePropagateWith(completionSource, cancellationToken, updateTask => completionSource.TrySetResult(updateTask.Result));
                    });

            completionSource.Task.ContinueRaiseSystemEventOnFault(_observer, EventForFailedOperation);

            return completionSource.Task;
        }

        public Task UpdateRoleInstanceCount(string serviceName, string roleName, string deploymentName, int instanceCount, CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var completionSource = new TaskCompletionSource<HttpStatusCode>();

            DoGetDeploymentConfiguration(client, serviceName, deploymentName, cancellationToken)
                .ContinuePropagateWith(completionSource, cancellationToken, queryTask =>
                    {
                        var config = queryTask.Result;
                        if (!UpdateInstanceCountConfig(config, roleName, instanceCount))
                        {
                            completionSource.TrySetResult(HttpStatusCode.NotModified);
                            return;
                        }

                        DoUpdateDeploymentConfiguration(client, serviceName, deploymentName, config, cancellationToken)
                            .ContinuePropagateWith(completionSource, cancellationToken, updateTask => completionSource.TrySetResult(updateTask.Result));
                    });

            completionSource.Task.ContinueRaiseSystemEventOnFault(_observer, EventForFailedOperation);

            return completionSource.Task;
        }

        public Task UpdateCurrentInstanceCount(AzureCurrentDeployment currentDeployment, string roleName, int instanceCount, CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var completionSource = new TaskCompletionSource<HttpStatusCode>();

            currentDeployment.Discover(cancellationToken)
                .ContinuePropagateWith(completionSource, cancellationToken, discoveryTask =>
                    DoGetDeploymentConfiguration(client, discoveryTask.Result.HostedServiceName, discoveryTask.Result.DeploymentName, cancellationToken)
                        .ContinuePropagateWith(completionSource, cancellationToken, queryTask =>
                            {
                                var config = queryTask.Result;
                                if (!UpdateInstanceCountConfig(config, roleName, instanceCount))
                                {
                                    completionSource.TrySetResult(HttpStatusCode.NotModified);
                                    return;
                                }

                                DoUpdateDeploymentConfiguration(client, discoveryTask.Result.HostedServiceName, discoveryTask.Result.DeploymentName, config, cancellationToken)
                                    .ContinuePropagateWith(completionSource, cancellationToken, updateTask => completionSource.TrySetResult(updateTask.Result));
                            }));

            completionSource.Task.ContinueRaiseSystemEventOnFault(_observer, EventForFailedOperation);

            return completionSource.Task;
        }

        bool UpdateInstanceCountConfig(XDocument configuration, string roleName, int newInstanceCount)
        {
            var instanceCountElement = GetInstanceCountConfigElement(configuration, roleName);
            var newInstanceCountString = newInstanceCount.ToString();

            if (instanceCountElement.Value == newInstanceCountString)
            {
                return false;
            }

            instanceCountElement.Value = newInstanceCountString;
            return true;
        }

        XAttribute GetInstanceCountConfigElement(XDocument xml, string roleName)
        {
            return xml.ServiceConfigElements("ServiceConfiguration", "Role")
                .Single(x => x.AttributeValue("name") == roleName)
                .ServiceConfigElement("Instances")
                .Attribute("count");
        }

        ICloudProvisioningEvent EventForFailedOperation(AggregateException exception)
        {
            HttpStatusCode httpStatus;
            if (ProvisioningErrorHandling.TryGetHttpStatusCode(exception, out httpStatus))
            {
                switch (httpStatus)
                {
                    case HttpStatusCode.Conflict:
                        return new ProvisioningFailedBecauseOfConflictEvent(exception);
                }

                return ProvisioningErrorHandling.IsTransientError(exception)
                    ? (ICloudProvisioningEvent)new ProvisioningFailedTransientEvent(exception, httpStatus)
                    : new ProvisioningFailedPermanentEvent(exception, httpStatus);
            }

            return ProvisioningErrorHandling.IsTransientError(exception)
                ? (ICloudProvisioningEvent)new ProvisioningFailedTransientEvent(exception)
                : new ProvisioningFailedPermanentEvent(exception);
        }

        public Task<int> GetLokadCloudWorkerCount(string serviceName, DeploymentSlot deploymentSlot, CancellationToken cancellationToken)
        {
            return GetRoleInstanceCount(serviceName, "Lokad.Cloud.WorkerRole", deploymentSlot, cancellationToken);
        }

        public Task<int> GetLokadCloudWorkerCount(string serviceName, string deploymentName, CancellationToken cancellationToken)
        {
            return GetRoleInstanceCount(serviceName, "Lokad.Cloud.WorkerRole", deploymentName, cancellationToken);
        }

        public Task<int> GetCurrentLokadCloudWorkerCount(AzureCurrentDeployment currentDeployment, CancellationToken cancellationToken)
        {
            return GetCurrentInstanceCount(currentDeployment, "Lokad.Cloud.WorkerRole", cancellationToken);
        }

        public Task UpdateLokadCloudWorkerCount(string serviceName, DeploymentSlot deploymentSlot, int instanceCount, CancellationToken cancellationToken)
        {
            return UpdateRoleInstanceCount(serviceName, "Lokad.Cloud.WorkerRole", deploymentSlot, instanceCount, cancellationToken);
        }

        public Task UpdateLokadCloudWorkerCount(string serviceName, string deploymentName, int instanceCount, CancellationToken cancellationToken)
        {
            return UpdateRoleInstanceCount(serviceName, "Lokad.Cloud.WorkerRole", deploymentName, instanceCount, cancellationToken);
        }

        public Task UpdateCurrentLokadCloudWorkerCount(AzureCurrentDeployment currentDeployment, int instanceCount, CancellationToken cancellationToken)
        {
            return UpdateCurrentInstanceCount(currentDeployment, "Lokad.Cloud.WorkerRole", instanceCount, cancellationToken);
        }

        Task<XDocument> DoGetDeploymentConfiguration(HttpClient client, string serviceName, string deploymentName, CancellationToken cancellationToken)
        {
            return client.GetXmlAsync<XDocument>(
                string.Format("services/hostedservices/{0}/deployments/{1}", serviceName, deploymentName),
                cancellationToken, _policies.RetryOnTransientErrors,
                (xml, tcs) => tcs.TrySetResult(xml.AzureElement("Deployment").AzureConfiguration()));
        }

        Task<XDocument> DoGetDeploymentConfiguration(HttpClient client, string serviceName, DeploymentSlot deploymentSlot, CancellationToken cancellationToken)
        {
            return client.GetXmlAsync<XDocument>(
                string.Format("services/hostedservices/{0}/deploymentslots/{1}", serviceName, deploymentSlot.ToString().ToLower()),
                cancellationToken, _policies.RetryOnTransientErrors,
                (xml, tcs) => tcs.TrySetResult(xml.AzureElement("Deployment").AzureConfiguration()));
        }

        Task<HttpStatusCode> DoUpdateDeploymentConfiguration(HttpClient client, string serviceName, string deploymentName, XDocument configuration, CancellationToken cancellationToken)
        {
            return client.PostXmlAsync<HttpStatusCode>(
                string.Format("services/hostedservices/{0}/deployments/{1}/?comp=config", serviceName, deploymentName),
                new XDocument(AzureXml.CreateElement("ChangeConfiguration", AzureXml.CreateConfiguration(configuration))),
                cancellationToken, _policies.RetryOnTransientErrors,
                (response, tcs) =>
                {
                    response.EnsureSuccessStatusCode();
                    tcs.TrySetResult(response.StatusCode);
                });
        }

        Task<HttpStatusCode> DoUpdateDeploymentConfiguration(HttpClient client, string serviceName, DeploymentSlot deploymentSlot, XDocument configuration, CancellationToken cancellationToken)
        {
            return client.PostXmlAsync<HttpStatusCode>(
                string.Format("services/hostedservices/{0}/deploymentslots/{1}/?comp=config", serviceName, deploymentSlot),
                new XDocument(AzureXml.CreateElement("ChangeConfiguration", AzureXml.CreateConfiguration(configuration))),
                cancellationToken, _policies.RetryOnTransientErrors,
                (response, tcs) =>
                {
                    response.EnsureSuccessStatusCode();
                    tcs.TrySetResult(response.StatusCode);
                });
        }
    }
}
