using System;
using System.Runtime.Serialization;

namespace DiskQueue.Core
{
    [DataContract]
    public class DiskQueueException : Exception
    {
        public DiskQueueException()
        {
        }

        public DiskQueueException(string message) : base(message)
        {
        }

        public DiskQueueException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}