using System;

namespace Gaev.DurableTask
{
    public class DelayResult
    {
        public DateTime Desired { get; set; }
        public DateTime Actual { get; set; }
        public bool OnTime { get; set; }
    }
}