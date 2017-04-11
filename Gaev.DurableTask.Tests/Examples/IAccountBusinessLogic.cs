using System.Threading.Tasks;

namespace Gaev.DurableTask.Tests.Examples
{
    public interface IAccountBusinessLogic
    {
        Task<string> Withdraw(string accountId, decimal amount);
        Task Capture(string accountId, string transactionId);
        Task DeleteTransaction(string accountId, string transactionId);
    }
}