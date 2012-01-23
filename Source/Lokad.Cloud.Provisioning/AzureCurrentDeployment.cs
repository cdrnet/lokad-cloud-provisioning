﻿#region Copyright (c) Lokad 2010-2011
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Cloud.Provisioning.Info;
using Lokad.Cloud.Provisioning.Instrumentation;
using Lokad.Cloud.Provisioning.Internal;

namespace Lokad.Cloud.Provisioning
{
    /// <summary>
    /// <para>
    /// Due to a an Azure runtime design decision it is hard to discover the absolute
    /// identity of the currently running hosted service instance (self; who-am-i).
    /// Yet the absolute identity is required to be able to manage it using the
    /// Azure management API, e.g. to automatically provision more worker instances on high demand.
    /// </para>
    /// <para>
    /// This class implements an async way to discover the absolute identity of the current
    /// hosted service and deployment. A successful discovery will be cached, subsequent
    /// calls to <see cref="Discover"/> will return the result immediately, or retry
    /// if the previous attempt had failed.
    /// </para>
    /// </summary>
    public class AzureCurrentDeployment
    {
        readonly string _subscriptionId;
        readonly X509Certificate2 _certificate;
        readonly string _deploymentPrivateId;
        readonly IProvisioningObserver _observer;

        readonly object _currentDeploymentDiscoveryLock = new object();
        Task<DeploymentReference> _currentDeploymentDiscoveryTask;
        DeploymentReference _currentDeployment;

        public AzureCurrentDeployment(string deploymentPrivateId, string subscriptionId, X509Certificate2 certificate, IProvisioningObserver observer = null)
        {
            _subscriptionId = subscriptionId;
            _certificate = certificate;
            _deploymentPrivateId = deploymentPrivateId;
            _observer = observer;
        }

        public Task<DeploymentReference> Discover(CancellationToken cancellationToken)
        {
            var client = HttpClientFactory.Create(_subscriptionId, _certificate);
            var completionSource = new TaskCompletionSource<DeploymentReference>();
            Task<DeploymentReference> previousTask;
            var discovery = new AzureDiscovery(_subscriptionId, _certificate, _observer);

            // If we have already succeeded, just pass on the result from the last time (shortcut)
            lock (_currentDeploymentDiscoveryLock)
            {
                if (_currentDeployment != null)
                {
                    completionSource.TrySetResult(_currentDeployment);
                    return completionSource.Task;
                }

                previousTask = _currentDeploymentDiscoveryTask;
                _currentDeploymentDiscoveryTask = completionSource.Task;
            }

            // If this is the first time this is called, create a new query and return
            if (previousTask == null)
            {
                discovery.DoDiscoverDeploymentAsync(client, _deploymentPrivateId, completionSource, cancellationToken);
                completionSource.Task.ContinueRaiseSystemEventOnFault(_observer, AzureDiscovery.EventForFailedOperation);
                return completionSource.Task;
            }

            // We have already called but have not got the result yet.
            // In case there is already a task running (this is likely in our scenarios) wait for the result.
            // Retry once in case it will fail, or, more importantly, if it has already failed (the last time).
            previousTask.ContinueWith(task =>
            {
                try
                {
                    if (task.IsFaulted || (task.IsCanceled && !cancellationToken.IsCancellationRequested))
                    {
                        discovery.DoDiscoverDeploymentAsync(client, _deploymentPrivateId, completionSource, cancellationToken);
                        completionSource.Task.ContinueRaiseSystemEventOnFault(_observer, AzureDiscovery.EventForFailedOperation);
                        return;
                    }

                    if (task.IsCanceled)
                    {
                        completionSource.TrySetCanceled();
                        return;
                    }

                    completionSource.TrySetResult(task.Result);
                }
                catch (Exception exception)
                {
                    // this should never happen, so forward but do not try to handle/retry here.
                    completionSource.TrySetException(exception);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            // NOTE: _currentDeployment may not be available yet in other continuations. This is ok.
            completionSource.Task.ContinueWith(t =>
            {
                lock (_currentDeploymentDiscoveryLock)
                {
                    _currentDeployment = t.Result;
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            return completionSource.Task;
        }
    }
}
