#region Copyright (c) Lokad 2010-2012
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using System;

namespace Lokad.Cloud.Provisioning.Instrumentation
{
    /// <summary>
    /// Cloud storage observer that implements a hot Rx Observable, forwarding all events synchronously
    /// (similar to Rx's FastSubject). Use this class if you want an easy way to observe Lokad.Cloud.Provisioning
    /// using Rx. Alternatively you can implement your own storage observer instead, or not use any observers at all.
    /// </summary>
    public class ProvisioningObserverSubject : IDisposable, IProvisioningObserver, IObservable<IProvisioningEvent>
    {
        readonly object _sync = new object();
        private bool _isDisposed;

        readonly IObserver<IProvisioningEvent>[] _fixedObservers;
        IObserver<IProvisioningEvent>[] _observers;

        /// <param name="fixedObservers">Optional externally managed fixed observers, will neither be completed nor disposed by this class.</param>
        public ProvisioningObserverSubject(IObserver<IProvisioningEvent>[] fixedObservers = null)
        {
            _fixedObservers = fixedObservers ?? new IObserver<IProvisioningEvent>[0];
            _observers = new IObserver<IProvisioningEvent>[0];
        }

        void IProvisioningObserver.Notify(IProvisioningEvent @event)
        {
            if (_isDisposed)
            {
                // make lifetime issues visible
                throw new ObjectDisposedException("CloudStorageInstrumentationSubject");
            }

            // Assuming event observers are light - else we may want to do this async

            foreach (var observer in _fixedObservers)
            {
                observer.OnNext(@event);
            }

            // assignment is atomic, no lock needed
            var observers = _observers;
            foreach (var observer in observers)
            {
                observer.OnNext(@event);
            }
        }

        public IDisposable Subscribe(IObserver<IProvisioningEvent> observer)
        {
            if (_isDisposed)
            {
                // make lifetime issues visible
                throw new ObjectDisposedException("CloudStorageInstrumentationSubject");
            }

            if (observer == null)
            {
                throw new ArgumentNullException("observer");
            }

            lock (_sync)
            {
                var newObservers = new IObserver<IProvisioningEvent>[_observers.Length + 1];
                Array.Copy(_observers, newObservers, _observers.Length);
                newObservers[_observers.Length] = observer;
                _observers = newObservers;
            }

            return new Subscription(this, observer);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _isDisposed = true;
                _observers = null;
            }
        }

        private class Subscription : IDisposable
        {
            private readonly ProvisioningObserverSubject _subject;
            private IObserver<IProvisioningEvent> _observer;

            public Subscription(ProvisioningObserverSubject subject, IObserver<IProvisioningEvent> observer)
            {
                _subject = subject;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_observer != null)
                {
                    lock (_subject._sync)
                    {
                        if (_observer != null && !_subject._isDisposed)
                        {
                            int idx = Array.IndexOf(_subject._observers, _observer);
                            if (idx >= 0)
                            {
                                var newObservers = new IObserver<IProvisioningEvent>[_subject._observers.Length + 1];
                                Array.Copy(_subject._observers, 0, newObservers, 0, idx);
                                Array.Copy(_subject._observers, idx + 1, newObservers, idx, _subject._observers.Length - idx - 1);
                                _subject._observers = newObservers;
                            }

                            _observer = null;
                        }
                    }
                }
            }
        }
    }
}
