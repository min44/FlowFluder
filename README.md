# FlowFluder
A simple project to demonstrate and understand the difference between a simple global state and a message based approach like MailBoxProcessor.

To get started:
```sh
dotnet run "agent"
```
or
```sh
dotnet run "simple"
```
Define amount of concurent threads, number of cycles and messages.
```fsharp
let numOfThreads  = 20
let numOfCycles   = 10
let numOfMessages = 50
let Interval      = 500
```
