namespace Gaev.DurableTask.Storage
{
    public class OperationState<T>
    {
        public T Value { get; set; }
        public ProcessException Exception { get; set; }
    }
}