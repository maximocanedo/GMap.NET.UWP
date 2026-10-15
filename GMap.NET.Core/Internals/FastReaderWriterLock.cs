using System;
using System.Threading;

namespace GMap.NET.Internals
{
    /// <summary>
    ///     Adaptación de FastReaderWriterLock usando ReaderWriterLockSlim para compatibilidad total con UWP.
    /// </summary>
    public sealed class FastReaderWriterLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public void AcquireReaderLock()
        {
            _lock.EnterReadLock();
        }

        public void ReleaseReaderLock()
        {
            _lock.ExitReadLock();
        }

        public void AcquireWriterLock()
        {
            _lock.EnterWriteLock();
        }

        public void ReleaseWriterLock()
        {
            _lock.ExitWriteLock();
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}