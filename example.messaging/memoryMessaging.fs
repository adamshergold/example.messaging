namespace Example.Messaging

open Microsoft.Extensions.Logging

open Example.Serialisation

type MemoryMessagingOptions = {
    Logger : ILogger option
    NumberOfMessagingThreads : int 
    TimeoutToWaitOnStopMilliseconds : int
    TimeoutToWaitForInflightWorkToCompleteMilliseconds : int
    SelfDeliver : bool
}
with 
    static member Default = {   
        Logger = None 
        NumberOfMessagingThreads = 1
        TimeoutToWaitOnStopMilliseconds = 500
        TimeoutToWaitForInflightWorkToCompleteMilliseconds = 1000
        SelfDeliver = false
    }
    
type MemoryMessaging( serialiser: ISerde, options: MemoryMessagingOptions ) =
    
    let logger = 
        options.Logger 
        
    let nActiveThreads = ref 0 

    let nInflight = ref 0 
    
    let allThreadsComplete = 
        new System.Threading.ManualResetEvent(false)
                
    let items = 
        new System.Collections.Concurrent.BlockingCollection<IEnvelope>() 
       
    let rand = 
        new System.Random() 
                 
    let recipients = 
        new System.Collections.Generic.Dictionary<RecipientId,IRecipient>()
                            
    static member Make( serialiser, options ) = 
        new MemoryMessaging( serialiser, options ) :> IMessaging

    member this.Send (envelope:IEnvelope) =
    
        if logger.IsSome then 
            logger.Value.LogDebug( "MemoryMessaging::Send - Sending to {Recipients} Subject={Subject} ReplyTo={ReplyTo}", envelope.Recipients, envelope.Message.Header.Subject, envelope.Message.Header.ReplyTo )
            
        items.Add( envelope ) 

    member this.Dispose () =
    
        if logger.IsSome then 
            logger.Value.LogDebug( "MemoryMessaging::Disposing" )
    
        this.Stop() 
    
    member this.Start () = 
        seq { 1 .. options.NumberOfMessagingThreads }
        |> Seq.iter ( fun idx ->
            let wt = new System.Threading.Thread( new System.Threading.ThreadStart( this.OnReceivedEnvelope ) )
            wt.Name <- sprintf "MemoryMessagingEnvelopeThread(%d)" idx
            //wt.IsBackground <- true // can't do this with .NET Core 
            wt.Start() )
        
    member this.Stop () =

        if logger.IsSome then 
            logger.Value.LogDebug( "MemoryMessaging::Stop" )
    
        // Wait for there to be no active work going-on    
        let noActiveWork = 
            new System.Threading.ManualResetEvent(false)

        let checkNoActiveWork state = 
            if !nInflight = 0 && items.Count = 0 then  
                noActiveWork.Set() |> ignore

        if logger.IsSome then 
            logger.Value.LogDebug( "MemoryMessaging::Stop - Waiting for inflight work to complete" )
                            
        let timer = 
            new System.Threading.Timer( new System.Threading.TimerCallback( checkNoActiveWork ), null, 0, 250 ) 

        if logger.IsSome then 
            logger.Value.LogDebug( "MemoryMessaging::Stop - Waiting for worker threads to exit" )
             
        noActiveWork.WaitOne( options.TimeoutToWaitForInflightWorkToCompleteMilliseconds ) |> ignore
        timer.Dispose()
                 
                 
        // Mark queue as complete                  
        items.CompleteAdding()
        allThreadsComplete.WaitOne( options.TimeoutToWaitOnStopMilliseconds ) |> ignore
        
    member this.AddRecipient (recipient:IRecipient) =   
        lock recipients ( fun _ ->
            if recipients.ContainsKey recipient.RecipientId then 
                failwithf "Cannot add duplicate recipient - %s" recipient.RecipientId.Id
            else    
                if logger.IsSome then 
                    logger.Value.LogDebug( "MemoryMessaging::AddRecipient - RecipientId {RecipientId} Label {Label}", recipient.RecipientId, recipient.Label )
            
                recipients.Add( recipient.RecipientId, recipient ) )    
        
    member this.RemoveRecipient (recipientId:RecipientId) = 
        lock recipients ( fun _ ->
            if not <| recipients.ContainsKey recipientId then 
                failwithf "Cannot remove non-existent recipient - %s" recipientId.Id
            else    
                if logger.IsSome then 
                    logger.Value.LogDebug( "MemoryMessaging::RemoveRecipient - RecipientId {RecipientId}", recipientId )
            
                recipients.Remove( recipientId ) )    

    member this.OnReceivedEnvelope () = 
    
        if logger.IsSome then 
            logger.Value.LogDebug( "MemoryMessaging::OnReceivedEnvelope - Thread starting {ThreadName}", System.Threading.Thread.CurrentThread.Name )
    
        System.Threading.Interlocked.Increment( nActiveThreads ) |> ignore 
        
        try 
            while not items.IsCompleted do
            
                let envelope = 
                    try 
                        let received = 
                            //items.Take( cts.Token )
                            items.Take()
                            
                        if logger.IsSome then 
                            logger.Value.LogDebug( "MemoryMessaging::OnReceivedEnvelope - Subject={Subject} Recipients={Recipients}", received.Message.Header.Subject, received.Recipients )
                            
                        Some received                            
                        
                    with 
                    | :? System.InvalidOperationException as ioe ->
                        None
                    | _ -> 
                        reraise()
                        
                        
                match envelope with 
                | Some e -> 
                    try
                        System.Threading.Interlocked.Increment( nInflight ) |> ignore
                        this.HandleEnvelope( e )
                    finally
                        System.Threading.Interlocked.Decrement( nInflight ) |> ignore
                | None -> 
                    ()
            
        with 
        | :? System.InvalidOperationException as ioe ->
            ()
        | :? System.OperationCanceledException as oc ->
            ()
                                        
        | _ -> 
            reraise() 
                 
        if logger.IsSome then 
            logger.Value.LogDebug( "MemoryMessaging::OnReceivedEnvelope - Thread finishing {ThreadName}", System.Threading.Thread.CurrentThread.Name )
                     
        System.Threading.Interlocked.Decrement( nActiveThreads ) |> ignore
                 
        if !nActiveThreads = 0 then
        
            if logger.IsSome then 
                logger.Value.LogDebug( "MemoryMessaging::OnReceivedEnvelope - No active threads remaining - signalling" )
         
            allThreadsComplete.Set() |> ignore 

    member this.HandleEnvelope (envelope:IEnvelope) =
    
        let allRecipients (label:string option) (includeMe:bool) =
        
            let all = 
                if includeMe then
                    recipients.Values |> Seq.cast
                else    
                    recipients.Values |> Seq.filter ( fun r -> r.RecipientId <> envelope.Sender ) 
                 
            if label.IsSome then 
                all |> Seq.filter ( fun r -> r.Label = label.Value ) |> Array.ofSeq 
            else 
                all |> Array.ofSeq                  

        let anyRecipient (label:string option) =
         
            let candidates = 
                allRecipients label options.SelfDeliver
                
            if candidates.Length = 0 then 
                Array.empty
            else    
                let idx = 
                    rand.Next(0,candidates.Length)
                [| candidates.[idx] |]
                
        let oneRecipient (rid:RecipientId) = 
            match recipients.TryGetValue rid with 
            | true, recipient -> Array.singleton recipient
            | false, _ -> Array.empty 
                     
        let deliverTo = 
            match envelope.Recipients with 
            | Recipients.ToAll(label) ->
                allRecipients label options.SelfDeliver
            | Recipients.ToAny(label) ->
                anyRecipient label
            | Recipients.ToOne(recipientId) ->
                oneRecipient recipientId 
                
        deliverTo
        |> Array.iter ( fun recipient ->
            if logger.IsSome then 
                logger.Value.LogDebug( "MemoryMessaging::HandleEnvelope - Delivering to {RecipientId}", recipient.RecipientId )
    
            recipient.Deliver envelope.Message )
         
     
    member this.CreateRecipient (label:string) (description:string) = 
    
        let recipient = 
            let recipientId = RecipientId.Make( System.Guid.NewGuid().ToString("N"), description ) 
            MemoryRecipient.Make( logger, label, recipientId, this :> IMessaging )
            
        this.AddRecipient recipient
        
        recipient             
                                                                                    
    interface System.IDisposable 
        with 
            member this.Dispose () = 
                this.Dispose() 
                                        
    interface IMessaging
        with   
            member this.CreateRecipient label description = 
                this.CreateRecipient label description

            member this.Start () = 
                this.Start()
                
            member this.Stop () =
                this.Stop() 
                                
            member this.AddRecipient recipient = 
                this.AddRecipient recipient 

            member this.RemoveRecipient recipientId = 
                this.RemoveRecipient recipientId
                
            member this.Send envelope = 
                this.Send envelope
         
