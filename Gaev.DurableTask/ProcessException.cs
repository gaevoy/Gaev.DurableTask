using System;

namespace Gaev.DurableTask
{
    public class ProcessException : Exception
    {
        public ProcessException(string message, string type) : base(message)
        {
            Type = type;
        }
        public string Type { get; }
    }
}