**Gaev.DurableTask** is tiny library to build durable task, saga, process manager using the async/await capabilities. Inspired by [Azure Durable Task Framework](https://github.com/Azure/durabletask). 

Just imagine you can write regular code using the async/await capabilities which can last for long time, say 1 week or year. Moreover, if an application crashes the durable task will resume execution exactly from where it left. 

A durable task must have some storage for storing current state in order to resume execution after restart/crash. There is MS SQL storage provider. However, you can implement your own provider, just implement *IProcessStorage*.

To estimate amount of used memory, a simple durable task was hosted in console application. As a result one instance of the durable task will use 4.3Kb in 32bit or 9KB in 64bit, so 250 000 instances will occupy 1Gb of 32bit console app.

Let's look closer to the durable task. It is easier to show an example:

**Saga, process manager** [complete example](https://github.com/gaevoy/Gaev.DurableTask/blob/master/Gaev.DurableTask.Tests/Examples/UserRegistrationSaga.cs)
```csharp
async Task DurableTask(string id, string email = null)
{
	using (var proc = _host.Spawn(id))
	{
		// Save email not to lose it if durable task resumes
		email = await proc.Get(email, "SaveEmail");
		// Register the user
		var userId = await proc.Do(() => _service.RegisterUser(email), "RegisterUser");
		// Generate a secret for email verification
		var secret = await proc.Get(Guid.NewGuid(), "secret");
		// Send email to the user with the secret to verify
		await proc.Do(() => _service.VerifyEmail(email, secret), "VerifyEmail");
		// Wait when user receive verification email and send the secret here, it can take couple of days
		await proc.Do(() => _service.WaitForEmailVerification(secret), "WaitForEmailVerification");
		// Activate the user in the system
		await proc.Do(() => _service.ActivateUser(userId), "ActivateUser");
	}
}
```

**Schedule** [complete example](https://github.com/gaevoy/Gaev.DurableTask/blob/master/Gaev.DurableTask.Tests/Examples/Schedule.cs)
```csharp
async Task DurableTask(string id, string email = null)
{
	using (var proc = _host.Spawn(id))
	{
		// Save email not to lose it if durable task resumes
		email = await proc.Get(email, "SaveEmail");
		await proc.Do(() => _smtp.Send(email, "Welcome!"), "Welcome");
		// Wait 1 month
		await proc.Delay(TimeSpan.FromDays(30), "Wait1m");
		await proc.Do(() => _smtp.Send(email, "Your 1st month with us. Congrats!"), "CongratsMonth");
		// Wait 11 months
		await proc.Delay(TimeSpan.FromDays(365 - 30), "Wait1y");
		await proc.Do(() => _smtp.Send(email, "Your 1st year with us. Congrats!"), "CongratsYear");
	}
}
```
**Rollback logic** [complete example](https://github.com/gaevoy/Gaev.DurableTask/blob/master/Gaev.DurableTask.Tests/Examples/MoneyTransferSaga.cs)
```csharp
async Task DurableTask(string id, string srcAccount = null, string destAccount = null, decimal amount = 0)
{
	using (var proc = _host.Spawn(id))
	{
		// Save values not to lose it if durable task resumes
		srcAccount = await proc.Get(srcAccount, "SaveSrcAccount");
		destAccount = await proc.Get(destAccount, "SaveDestAccount");
		amount = await proc.Get(amount, "SaveAmount");
		var srcTranId = Guid.Empty;
		var destTranId = Guid.Empty;
		try
		{
			// Start transferring the money
			srcTranId = await proc.Do(() => _service.StartTransfer(srcAccount, -amount), "StartTransfer1");
			destTranId = await proc.Do(() => _service.StartTransfer(destAccount, +amount), "StartTransfer2");
			// Complete transferring the money
			await proc.Do(() => _service.CompleteTransfer(srcAccount, srcTranId), "CompleteTransfer1");
			await proc.Do(() => _service.CompleteTransfer(destAccount, destTranId), "CompleteTransfer2");
		}
		catch (ProcessException ex) when (ex.Type == nameof(TransferFailedException))
		{
			// Rollback logic
			if (srcTranId != Guid.Empty)
				await proc.Do(() => _service.RollbackTransfer(srcAccount, srcTranId), "RollbackTransfer1");
			if (destTranId != Guid.Empty)
				await proc.Do(() => _service.RollbackTransfer(destAccount, destTranId), "RollbackTransfer2");
			throw;
		}
	}
}
```
**Sending a message to the durable task** [complete example](https://github.com/gaevoy/Gaev.DurableTask/tree/master/Gaev.DurableTask.ConsolePlayground)
```csharp
async Task DurableTask(string processId, string companyId = null, string creditCard = null)
{
	using (var proc = _host.Spawn(processId).As<CreditCardProcess>())
	{
		companyId = await proc.Get(companyId, "1");
		creditCard = await proc.Get(creditCard, "2");
		Console.WriteLine($"CreditCardFlow is up for companyId={companyId} creditCard={creditCard}");
		var email = await proc.Do(() => GetEmail(companyId), "3");
		await proc.Do(() => SendEmail(email, $"{creditCard} was assigned to you"), "4");
		var onCheckTime = proc.Delay(TimeSpan.FromMinutes(5), "5");
		var onFirstTransaction = proc.Do(() => proc.OnTransactionAppeared(), "6");
		var onDeleted = proc.Do(() => proc.OnCreditCardDeleted(), "7");
		Task.Run(async () =>
		{
			await onCheckTime;
			if (onDeleted.IsCompleted) return;
			if (!onFirstTransaction.IsCompleted)
				await proc.Do(() => SendEmail(email, $"{creditCard} is inactive long time"), "8");
		});
		Task.Run(async () =>
		{
			await onFirstTransaction;
			if (onDeleted.IsCompleted) return;
			await proc.Do(() => SendEmail(email, $"{creditCard} received 1st transaction"), "9");
		});

		await onDeleted;
		// Cancel all pending tasks
		await proc.Do(() => SendEmail(email, $"{creditCard} was deleted"), "10");
	}
}
static void Main(string[] args)
{
	...
	var procId = creditCardFlow.Start("1111-1111-1111-1111", "user1@gmail.com");
	host.Get(procId).As<CreditCardProcess>()?.RaiseOnTransactionAppeared();
	host.Get(procId).As<CreditCardProcess>()?.RaiseOnCreditCardDeleted();
	...
}
```
