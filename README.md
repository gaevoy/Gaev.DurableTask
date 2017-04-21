**Gaev.DurableTask** is tiny library to build durable task, saga, process manager using the async/await capabilities. Inspired by [Azure Durable Task Framework](https://github.com/Azure/durabletask). 

Just imagine you can write regular code using the async/await capabilities which can last for long time, say 1 week or year. Moreover, if an application crashes the durable task will resume execution exactly from where it left. 

A durable task must have some storage for storing current state in order to resume execution after restart/crash. There is MS SQL storage provider. However, you can implement your own provider, just implement *IProcessStorage*.

To estimate amount of used memory, a simple durable task was hosted in console application. As a result one instance of the durable task will use 4.3Kb in 32bit or 9KB in 64bit, so 250 000 instances will occupy 1Gb of 32bit console app.

Let's look closer to the durable task. It is easier to show an example:

**Saga, process manager**
```csharp
///...
```
