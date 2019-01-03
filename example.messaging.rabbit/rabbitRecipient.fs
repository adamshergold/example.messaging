namespace Example.Messaging.Rabbit

open Microsoft.Extensions.Logging

open RabbitMQ.Client 

open Example.Serialisation

open Example.Messaging
open Example.Messaging.Rabbit.Common

[<AutoOpen>]
module RabbitRecipientImpl =
    
    let RootException (ex:System.Exception) =
    
        let rec impl (ex:System.Exception) = 
            if ex.InnerException <> null then impl ex.InnerException else ex
            
        impl ex             
        
type RabbitRecipientOptions = {
    Logger : ILogger option
    Label : string
    RecipientId : RecipientId 
    Messaging : IMessaging 
    Channel : IModel
    ToAll : RabbitEndPoint
    ToOne : RabbitEndPoint
    ToAny: RabbitEndPoint
    SelfDeliver : bool
}
with 
    static member Make( logger, label, id, messaging, channel, toAll, toOne, toAny, selfDeliver ) = 
        { Logger = logger; Label = label; RecipientId = id; Messaging = messaging; Channel = channel; ToAll = toAll; ToOne = toOne; ToAny = toAny; SelfDeliver = selfDeliver } 
        
type RabbitRecipient( serialiser: ISerde, options: RabbitRecipientOptions ) as this =
    inherit BaseRecipient( options.Logger, options.Label, options.RecipientId, options.Messaging )  
             
    let consumerAllOne = 
        Events.EventingBasicConsumer( options.Channel ) 

    let consumerAny = 
        Events.EventingBasicConsumer( options.Channel ) 

    do  
    
        let recipientQueue = 
            options.Channel.QueueDeclare( queue = options.RecipientId.Id, durable = false, autoDelete = true ) 

        options.Channel.QueueBind( recipientQueue.QueueName, options.ToAll.Exchange, options.Label )
        options.Channel.QueueBind( recipientQueue.QueueName, options.ToOne.Exchange, options.RecipientId.Id )
                            
        consumerAllOne.Received.Add( this.OnReceivedAllOne )

        options.Channel.BasicConsume(
            queue = recipientQueue.QueueName,
            autoAck = false,
            consumer = consumerAllOne ) |> ignore

        let sharedQueue = 
            options.Channel.QueueDeclare( queue = options.Label, durable = false, autoDelete = false, exclusive = false )
            
        options.Channel.QueueBind( sharedQueue.QueueName, options.ToAny.Exchange, options.Label )
        
        consumerAny.Received.Add( this.OnReceivedAny )
        
        options.Channel.BasicConsume(
            queue = sharedQueue.QueueName,
            autoAck = false,
            consumer = consumerAny ) |> ignore

    member val Serialiser = serialiser 
    
    member val Messaging = options.Messaging 
    
    member val Logger = options.Logger
    
    static member Make( serialiser, options ) = 
        new RabbitRecipient( serialiser, options ) :> IRecipient

    member this.OnReceivedImpl (args:Events.BasicDeliverEventArgs) =

        let contentType, typeName = 
            args.BasicProperties.ContentType, args.BasicProperties.Type 

        if this.Logger.IsSome then 
            this.Logger.Value.LogTrace( "RabbitRecipient::OnReceivedImpl - RecipientId {RecipientId} {ContentType} {TypeName}", this.RecipientId, contentType, typeName )
    
        match Helpers.Deserialise this.Serialiser (Some contentType) typeName args.Body with 
        | :? IEnvelope as e ->
        
            if this.Logger.IsSome then 
                this.Logger.Value.LogTrace( "RabbitRecipient::OnReceivedImpl - RecipientId {RecipientId} CorrelationId {CorrelationId} From {Sender}", this.RecipientId, e.Message.CorrelationId, e.Sender )
        
            if options.SelfDeliver || e.Sender <> this.RecipientId then 
                this.Deliver e.Message
        | _ as v -> 
            if this.Logger.IsSome then 
                this.Logger.Value.LogError( "RabbitRecipient::OnReceivedImpl - RecipientId {RecipientId} Expected IEnvelope but saw {Message}", v.GetType() )
            ()            

    member this.OnReceivedAllOne (args:Events.BasicDeliverEventArgs) =
    
        try
            this.OnReceivedImpl args 
            
            if this.Logger.IsSome then 
                this.Logger.Value.LogTrace( "RabbitRecipient::OnReceivedAllOne - Acknowledge" )
            
            options.Channel.BasicAck( args.DeliveryTag, multiple = false )
        with 
        | _ as ex ->
            if this.Logger.IsSome then 
                let ex = RootException ex
                this.Logger.Value.LogError( "RabbitRecipient::OnReceivedAllOne - Exception! {Message} {Stack}", ex.Message, ex.StackTrace )
            
    member this.OnReceivedAny (args:Events.BasicDeliverEventArgs) =

        try    
            this.OnReceivedImpl args 
            
            if this.Logger.IsSome then 
                this.Logger.Value.LogTrace( "RabbitRecipient::OnReceivedAny - Acknowledge" )
            
            options.Channel.BasicAck( args.DeliveryTag, multiple = false )
        with 
        | _ as ex ->
            if this.Logger.IsSome then
                let ex = RootException ex
                this.Logger.Value.LogError( "RabbitRecipient::OnReceivedAny - Exception! {Message} {Stack}", ex.Message, ex.StackTrace )
        
    member this.Send (recipients:Recipients,msg:IMessage) = 
        base.Send(recipients,msg)
        Envelope.Make( this.RecipientId, recipients, msg ) |> this.Messaging.Send

    member this.Dispose () =
    
        if this.Logger.IsSome then 
            this.Logger.Value.LogTrace( "RabbitRecipient::Dispose" )
    
        base.Dispose()

    member this.Deliver (message:IMessage) =
     
        if this.Logger.IsSome then 
            this.Logger.Value.LogTrace( "RabbitRecipient::Deliver - RecipientId {RecipientId} Header {Header} #Receivers {nReceivers}", this.RecipientId, message.Header, this.Receivers |> Seq.length )
     
        base.Deliver(message)
        
        let replies =
            this.Receivers 
            |> Seq.map ( fun receiver ->
                if receiver.Subject = message.Header.Subject then 
                    Some receiver 
                else
                    None )
            |> Seq.choose ( fun x -> x )
            |> Seq.map ( fun receiver ->
        
                if this.Logger.IsSome then 
                    this.Logger.Value.LogTrace( "RabbitRecipient::Deliver - RecipientId {RecipientId} Calling OnHandler ReceiverId {ReceiverId}", this.RecipientId, receiver.ReceiverId )
            
                receiver.OnHandler message )
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Seq.choose ( fun x -> x )

                             
        replies 
        |> Seq.iter ( fun (recipients,message) ->  
        
            if this.Logger.IsSome then 
                this.Logger.Value.LogDebug( "RabbitRecipient::Deliver - Replying to {Recipients}", recipients )
                
            this.Send (recipients,message) )                                
                                                                            
    interface System.IDisposable 
        with 
            member this.Dispose () = 
                this.Dispose() 
                                        
    interface IRecipient
        with
            member this.Deliver message = 
                this.Deliver message 
                
            member this.Send (recipients,msg) = 
                this.Send (recipients,msg)
                                             
    