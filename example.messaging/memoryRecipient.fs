namespace Example.Messaging

open Microsoft.Extensions.Logging

type MemoryRecipient( logger: ILogger option, label:string, id: RecipientId, messaging: IMessaging ) = 
    inherit BaseRecipient( logger, label, id, messaging )

    member val RecipientId = id 
    
    static member Make( logger, label, id, messaging ) = 
        new MemoryRecipient( logger, label, id, messaging ) :> IRecipient

    member this.Send (recipients:Recipients,msg:IMessage) =
        base.Send(recipients,msg)
        Envelope.Make( this.RecipientId, recipients, msg ) |> messaging.Send

    member this.Deliver (message:IMessage) =
    
        if logger.IsSome then 
            logger.Value.LogTrace( "MemoryRecipient::Deliver - RecipientId {RecipientId} Message {Message} ({nReceivers} #Receivers)", this.RecipientId, message, this.Receivers |> Seq.length ) 
            
        base.Deliver(message) 
         
        let replies =
            this.Receivers
            |> Seq.map ( fun receiver ->
                if receiver.Subject = message.Header.Subject then
                 
                    if logger.IsSome then 
                        logger.Value.LogTrace( "MemoryRecipient::Deliver - Calling onHandler for {ReceiverId}", receiver.ReceiverId ) 

                    receiver.OnHandler message  
                else
                    None )
            |> Seq.choose ( fun x -> x )
             
        replies 
        |> Seq.iter ( fun (recipients,message) ->  
        
            if logger.IsSome then 
                logger.Value.LogDebug( "MemoryRecipient::Deliver - Replying to {Recipients}", recipients )
                
            this.Send (recipients,message) )                                
                                                                            
    interface IRecipient
        with
            member this.Deliver message = 
                this.Deliver message
                 
            member this.Send (recipients,msg) = 
                this.Send (recipients,msg)
                                             
    