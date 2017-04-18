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
            using (var host = new ProcessHost(new MsSqlProcessStorage(connectionString)))
            {
                var companyId = Guid.NewGuid().ToString();
                var creditCard = "111";
                var creditCardFlow = new CreditCardFlow(host);
                creditCardFlow.RegisterProcess();
                host.Start().Wait();
                if (host.Get(nameof(CreditCardFlow) + creditCard) == null)
                    creditCardFlow.Start(companyId, creditCard);
                //host.Get(nameof(CreditCardFlow) + "111").As<CreditCardProcess>()?.RaiseOnTransactionAppeared();
                //host.Get(nameof(CreditCardFlow) + "111").As<CreditCardProcess>()?.RaiseOnCreditCardDeleted();
                Console.Read();
            }
        }
    }
}
