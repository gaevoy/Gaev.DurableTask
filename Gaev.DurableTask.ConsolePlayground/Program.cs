using System;
using System.Configuration;
using Gaev.DurableTask.MsSql;

namespace Gaev.DurableTask.ConsolePlayground
{
    class Program
    {
        static void Main(string[] args)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["Sql"].ConnectionString;
            var host = new ProcessHost(new MsSqlProcessStorage(connectionString));

            var companyId = Guid.NewGuid().ToString();
            var creditCard = "555";
            var creditCardFlow = new CreditCardFlow(host);
            creditCardFlow.RegisterProcess();
            var _ = host.Run();
            //creditCardFlow.Start(companyId, creditCard);
            //((CreditCardFlow.MyProcess)host.Spawn(nameof(CreditCardFlow) + creditCard)).RaiseOnTransactionAppeared();
            ((CreditCardFlow.MyProcess)host.Spawn(nameof(CreditCardFlow) + "333")).RaiseOnCreditCardDeleted();
            Console.Read();
        }
    }
}
