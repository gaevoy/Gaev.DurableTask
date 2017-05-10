namespace Gaev.DurableTask.FSharpConsoleApp
open System
open System.Threading.Tasks
open Gaev.DurableTask
module App =

    type Microsoft.FSharp.Control.AsyncBuilder with
      member x.Bind(t:Task<'T>, f:'T -> Async<'R>) : Async<'R>  = 
        async.Bind(Async.AwaitTask t, f)

    type CreditCardProcess(proc : IProcess) = 
        inherit ProcessWrapper(proc)
        let onTransactionAppeared = new TaskCompletionSource<obj>();
        let onCreditCardDeleted = new TaskCompletionSource<obj>();
        do
            proc.Cancellation.Register(Action<obj>(fun _ -> (
                onTransactionAppeared.TrySetCanceled() |> ignore
                onCreditCardDeleted.TrySetCanceled() |> ignore
            )), null) |> ignore
        member this.RaiseOnTransactionAppeared() = onTransactionAppeared.TrySetResult(null)
        member this.OnTransactionAppeared() = onTransactionAppeared.Task :> Task
        member this.RaiseOnCreditCardDeleted() = onCreditCardDeleted.TrySetResult(null)
        member this.OnCreditCardDeleted() = onCreditCardDeleted.Task :> Task

    type CreditCardFlow(host : IProcessHost) =

        let GetEmail(companyId: string) = 
            async {
                do! Async.Sleep(5); // Emulate async
                return companyId + "@test.com";
            } |> Async.StartAsTask     
            
        let SendEmail(email: string, text: string) = 
            async {
                do! Async.Sleep(5); // Emulate async
                Console.WriteLine("Email '"+text+"' was sent to "+email);
            } |> Async.StartAsTask

        let DurableTask(processId:string, creditCard: string, companyId: string) = 
            async {
                use proc = host.Spawn(processId).As<CreditCardProcess>()
                let! companyId = proc.Get(companyId, "1")
                let! creditCard = proc.Get(creditCard, "2")
                Console.WriteLine("CreditCardFlow is up for companyId="+companyId+" creditCard="+creditCard);
                let! email = proc.Do((fun () -> GetEmail(companyId)), "3");
                do! proc.Do((fun () -> SendEmail(email, creditCard+" was assigned to you")), "4");
                let onDeleted = proc.Do((fun() -> proc.OnCreditCardDeleted()), "7");
                do! onDeleted |> Async.AwaitTask
                do! proc.Do((fun () -> SendEmail(email, creditCard+" was deleted")), "10");
                return ()
            } |> Async.StartAsTask

        member this.Start(creditCard: string, companyId: string) =
            let processId = "CreditCardFlow" + creditCard
            DurableTask(processId, companyId, creditCard) |> host.Watch
            processId

        member this.RegisterProcess() =
            new ProcessRegistration(
                IdSelector = (fun id -> id.StartsWith("CreditCardFlow")),
                EntryPoint = (fun id -> DurableTask(id,null,null) :> Task),
                ProcessWrapper = (fun p -> new CreditCardProcess(p) :> IProcess)
            ) |> host.Register
