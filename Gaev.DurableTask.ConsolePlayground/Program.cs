using System;
using System.Configuration;
using System.Data.SqlClient;
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
                var creditCardFlow = new CreditCardFlow(host);
                creditCardFlow.RegisterProcess();
                host.Start().Wait();
                Console.WriteLine(@"Type following commands:
 exit - To stop host and exit
 add {creditCard} {companyId} - To add a credit card for a company
 tran {creditCard} - To make transaction to the credit card
 delete {creditCard} - To delete the credit card");
                while (true)
                {
                    var command = (Console.ReadLine() ?? "").Split(' ');
                    if (command[0] == "exit")
                    {
                        SqlConnection.ClearAllPools(); // to speed up exit
                        return;
                    }
                    if (command[0] == "add" && command.Length == 3)
                        if (host.Get(nameof(CreditCardFlow) + command[1]) == null)
                            creditCardFlow.Start(command[1], command[2]);
                    if (command[0] == "tran" && command.Length == 2)
                        host.Get(nameof(CreditCardFlow) + command[1]).As<CreditCardProcess>()?.RaiseOnTransactionAppeared();
                    if (command[0] == "delete" && command.Length == 2)
                        host.Get(nameof(CreditCardFlow) + command[1]).As<CreditCardProcess>()?.RaiseOnCreditCardDeleted();
                }
            }
        }
    }
}
