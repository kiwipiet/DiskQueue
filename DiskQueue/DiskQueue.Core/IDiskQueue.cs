using System;

namespace DiskQueue.Core
{
    public interface IDiskQueue : IDisposable
    {
        void Enqueue<T>(T item) where T : class;
        T Dequeue<T>() where T : class;
        T Peek<T>() where T : class;
        QueueInfo GetQueueInfo();
        void CleanQueue();
    }
}