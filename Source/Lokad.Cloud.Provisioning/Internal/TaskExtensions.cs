#region Copyright (c) Lokad 2010-2011
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Cloud.Provisioning.Instrumentation;
using Lokad.Cloud.Provisioning.Instrumentation.Events;

namespace Lokad.Cloud.Provisioning.Internal
{
    internal static class TaskExtensions
    {
        /// <remarks>Only put short operations in this continuation, or do them async, as the continuation is executed synchronously.</remarks>
        internal static void ContinuePropagateWith<TCompletion, TTask>(this Task<TTask> task, TaskCompletionSource<TCompletion> completionSource, CancellationToken cancellationToken, Action<Task<TTask>> handleCompleted)
        {
            task.ContinueWith(t =>
                {
                    try
                    {
                        if (t.IsFaulted)
                        {
                            var baseException = t.Exception.GetBaseException();

                            if (cancellationToken.IsCancellationRequested && baseException is HttpException)
                            {
                                // If cancelled: HttpExceptions are assumed to be caused by the cancellation, hence we ignore them and cancel.
                                completionSource.TrySetCanceled();
                            }
                            else
                            {
                                completionSource.TrySetException(baseException);
                            }
                            return;
                        }

                        if (t.IsCanceled)
                        {
                            completionSource.TrySetCanceled();
                            return;
                        }

                        handleCompleted(t);
                    }
                    catch (Exception exception)
                    {
                        completionSource.TrySetException(exception);
                    }

                }, TaskContinuationOptions.ExecuteSynchronously);
        }

        internal static void ContinueRaiseSystemEventOnFault(this Task task, IProvisioningObserver observer, Func<AggregateException, IProvisioningEvent> handler)
        {
            task.ContinueWith(t =>
                {
                    if (!t.IsFaulted)
                    {
                        return;
                    }

                    var exception = t.Exception;
                    if (exception == null || observer == null)
                    {
                        return;
                    }

                    try
                    {
                        observer.Notify(handler(exception));
                    }
// ReSharper disable EmptyGeneralCatchClause
                    catch
// ReSharper restore EmptyGeneralCatchClause
                    {
                        // Suppression is intended: we can't log but also don't want to tear down just because of a failed notification.
                        // Also, this is not where exceptions are handled in the first place, since the failed tasks are returned to the application.
                    }
                }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
