open System
open System.Threading.Tasks
open Gaev.DurableTask
open Gaev.DurableTask.Storage
open Gaev.DurableTask.FSharpConsoleApp.App

[<EntryPoint>]
let main argv = 
    use host = new ProcessHost(new FileSystemProcessStorage())
    let creditCardFlow = new CreditCardFlow(host)
    creditCardFlow.RegisterProcess()
    host.Resume()
    Console.WriteLine(@"Type following commands:
 exit - To stop host and exit
 add {creditCard} {companyId} - To add a credit card for a company
 tran {creditCard} - To make transaction to the credit card
 delete {creditCard} - To delete the credit card")
    let mutable continueLooping = true
    while continueLooping do
        let input = Console.ReadLine()
        let command = (match input with null -> "" | _ -> input).Split(' ')
        if String.Equals(command.[0], "exit") then
            Console.WriteLine("exiting...")
            continueLooping <- false
        if String.Equals(command.[0], "add") && host.Get("CreditCardFlow" + command.[1]) = null then
            creditCardFlow.Start(command.[1], command.[2]) |> ignore
        if String.Equals(command.[0], "delete") && host.Get("CreditCardFlow" + command.[1]) <> null then
            host.Get("CreditCardFlow" + command.[1]).As<CreditCardProcess>().RaiseOnCreditCardDeleted() |> ignore

    0 // return an integer exit code
