namespace Example.Messaging

open Microsoft.Extensions.Logging

type BaseRecipient( logger: ILogger option, label: string, id: RecipientId, messaging: IMessaging ) =

    let nSent = ref 0 
    
    let nReceived = ref 0 
    
    let receivers = 
        new System.Collections.Generic.Dictionary<ReceiverId,Receiver>()
        
    member val Label = label 
            
    member val RecipientId = id 
    
    member val Receivers = receivers.Values |> Seq.cast 
    
    member this.Statistics 
        with get () = Statistics.Make( !nSent, !nReceived ) 

    member this.Dispose () = 
        receivers.Clear() 
                
    member this.Deliver message = 
        System.Threading.Interlocked.Increment( nReceived ) |> ignore
        
    member this.AddReceiver (receiver:Receiver) =   
        lock receivers ( fun _ ->   
            if receivers.ContainsKey receiver.ReceiverId then 
                failwithf "Cannot add duplicate receiver - %s" receiver.ReceiverId 
            else 
                if logger.IsSome then 
                    logger.Value.LogTrace( "BaseRecipient::AddReceiver RecipientId {RecipientId} ReceiverId {Receiver}", this.RecipientId, receiver.ReceiverId )
            
                receivers.Add( receiver.ReceiverId, receiver ) )
        
    member this.RemoveReceiver (receiverId:ReceiverId) = 
        lock receivers ( fun _ ->   
            if not <| receivers.ContainsKey receiverId then 
                failwithf "Cannot remove non-existent receiver - %s" receiverId 
            else 
                if logger.IsSome then 
                    logger.Value.LogTrace( "BaseRecipient::RemoveReceiver RecipientId {RecipientId} ReceiverId {Receiver}", this.RecipientId, receiverId )
            
                receivers.Remove( receiverId ) )

    member this.Send (recipients,message) = 
        System.Threading.Interlocked.Increment( nSent ) |> ignore 
                                        
    interface System.IDisposable 
        with 
            member this.Dispose () = 
                this.Dispose() 
                                        
    interface IRecipient
        with
            member this.Label = this.Label 
            
            member this.RecipientId = this.RecipientId 
            
            member this.Deliver message = 
                this.Deliver message 
                
            member this.AddReceiver receiver = 
                this.AddReceiver receiver 

            member this.RemoveReceiver receiverId = 
                this.RemoveReceiver receiverId 

            member this.Receivers = this.Receivers
            
            member this.Send (recipients,msg) = 
                this.Send (recipients,msg)
                
            member this.Statistics = this.Statistics
                                             
    