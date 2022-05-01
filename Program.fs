open System
open System.Threading
open Microsoft.FSharp.Control
open Microsoft.FSharp.Core
open Microsoft.FSharp.Collections

type Item =
    { Id: int
      Name: string}
    static member Create() =
        { Id = Random().Next(0, 99)
          Name = Guid.NewGuid().ToString() }

type State =
    { Counter: int
      Items: Item list }

type Mode =
    | Simple
    | Agent

type Msg =
    | Increment
    | Decrement
    | AddItems of Item List
    | GetState of AsyncReplyChannel<State>
    
    
type SimpleGlobal(init) =
    member val State = init with get, set
    member x.Post msg =
        match msg with
        | Increment -> x.State <- { x.State with Counter = x.State.Counter + 1 }
        | Decrement -> x.State <- { x.State with Counter = x.State.Counter - 1 }
        | AddItems items -> x.State <- { x.State with Items = x.State.Items @ items }
        | GetState _ -> ()


let CreateProcessor init =
    let body (inbox: MailboxProcessor<Msg>) =
        let rec loop state =
            async {
                match! inbox.Receive() with
                | Increment  -> do! loop { state with Counter = state.Counter + 1}
                | Decrement  -> do! loop { state with Counter = state.Counter - 1 }
                | AddItems items -> do! loop { state with Items = state.Items @ items } 
                | GetState reply -> reply.Reply state; do! loop state }
        loop init
    MailboxProcessor<Msg>.Start body


let RunThreads number operations =
    let asyncSetter operations () =
        async { do! operations() }
        |> Async.Start
    [0..number-1]
    |> Seq.map(fun x ->
        async {
            let thread = Thread(asyncSetter operations)
            thread.Start()
            if thread.IsAlive
            then printfn $"Running thread={x}.{thread.ManagedThreadId}"
        })
    |> Async.Sequential
    |> Async.Ignore
    |> Async.RunSynchronously


let CyclicCall number (interval: int) operation () =
    async {
        for _ in 0..number-1 do
        do! Async.Sleep interval
        operation() }


let RunTest threadsCount operationsCount interval operations =
    CyclicCall
        operationsCount
        interval
        operations
    |> RunThreads threadsCount


let InitAgent msgGenerator numOfMsg =
    let init = { Counter = 0; Items = [] }
    let simple = SimpleGlobal init
    let agent  = CreateProcessor init
    function
    | Simple ->
        (msgGenerator simple.Post numOfMsg),
        (fun () -> simple.State),
        (fun () -> 0)
    | Agent  ->
        (msgGenerator agent.Post numOfMsg),
        (fun () -> agent.PostAndReply GetState),
        (fun () -> agent.CurrentQueueLength)


let MsgGenerator msg count () =
    [0..count-1]
    |> Seq.iter(fun _ ->
        msg <| Increment 
        msg <| AddItems [ Item.Create() ])


let ValidateArg =
    function
    | "simple" -> Simple
    | "agent"  -> Agent
    | arg -> failwith $"Wrong argument: {arg}" 


let GetInfo getState getQueue =
    async {
        while true do
            printf $"Queue={getQueue()}; "
            printf $"Counter={getState().Counter}; "
            printfn $"ItemsCount={getState().Items.Length}"
            do! Async.Sleep 1000
        } |> Async.StartImmediate


[<EntryPoint>]
let Main args =
    let mode = ValidateArg args[0]
    
    let numOfThreads  = 20
    let numOfCycles   = 10
    let numOfMessages = 50
    let Interval      = 500
    
    let operations,
        getState,
        getQueue =
            InitAgent
                MsgGenerator
                numOfMessages
                mode
    
    RunTest
        numOfThreads
        numOfCycles
        Interval
        operations
    
    GetInfo
        getState
        getQueue
    
    Console.ReadKey true |> ignore
        
    printfn $"Result: ItemsCount={getState().Items.Length}"
    
    0