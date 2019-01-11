namespace Example.Messaging.Rabbit

open Microsoft.Extensions.Logging

open Example.Serialisation

open Example.Messaging
open Example.Messaging.Rabbit.Common 

open RabbitMQ.Client 


[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]        
type RabbitMessagingOptions = {
    Logger : ILogger option
    Factory : Common.FactoryOptions
    ToAll : RabbitEndPoint
    ToOne : RabbitEndPoint
    ToAny : RabbitEndPoint
    NumberOfOutboxThreads : int
    ContentType : string 
    SelfDeliver : bool
}
with 
    static member Default = {   
        Logger = None 
        Factory = Common.FactoryOptions.Default
        ToAll = RabbitEndPoint.Make( "example.toAll", None )
        ToOne = RabbitEndPoint.Make( "example.toOne", None )
        ToAny = RabbitEndPoint.Make( "example.toAny", None )
        NumberOfOutboxThreads = 1
        ContentType = "binary"
        SelfDeliver = false
    }
    
[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]    
type RabbitMessaging( serialiser: ISerde, options: RabbitMessagingOptions ) =
    
    let logger = 
        options.Logger
        
    let factory = 
        Common.Factory.Make( { options.Factory with Logger = options.Logger } ) 
                
    let connectionManager = 
        Common.ConnectionManager.Make( logger, factory ) 
                     
    let connection = 
        connectionManager.CreateConnection() 
                             
    let channel = 
        connection.CreateModel()

    let outbox = 
        new System.Collections.Concurrent.BlockingCollection<IEnvelope>() 
         
    static member Make( serialiser, options ) = 
        new RabbitMessaging( serialiser, options ) :> IMessaging

    member this.Send (envelope:IEnvelope) =
    
        if logger.IsSome then 
            logger.Value.LogDebug( "RabbitMessaging::Sending from {Sender} to {Recipients} Subject={Subject} ReplyTo={ReplyTo}", envelope.Sender, envelope.Recipients, envelope.Message.Header.Subject, envelope.Message.Header.ReplyTo )
           
        let properties = 
            channel.CreateBasicProperties()
            
        let typeSerialiser = 
            serialiser.TrySerdeBySystemType (options.ContentType,envelope.GetType())

        if typeSerialiser.IsNone then 
            failwithf "Unable to find serialiser for [%O] / [%s]" (envelope.GetType()) options.ContentType 
                                
        properties.Type <- typeSerialiser.Value.TypeName 
        properties.ContentType <- options.ContentType
        
        let bytes = 
            Helpers.Serialise serialiser (options.ContentType) envelope 
            
        match envelope.Recipients with 
        | Recipients.ToAll(label) ->     
            let routingKey = if label.IsSome then label.Value else ""       
            channel.BasicPublish( options.ToAll.Exchange, routingKey = routingKey, body = bytes, basicProperties = properties )    
        | Recipients.ToOne(recipient) ->            
            channel.BasicPublish( options.ToOne.Exchange, recipient.Id, body = bytes, basicProperties = properties )    
        | Recipients.ToAny(label) ->
            let rk = if label.IsSome then label.Value else ""
            channel.BasicPublish( exchange = options.ToAny.Exchange, routingKey = rk, body = bytes, basicProperties = properties )

    member this.Dispose () =
    
        if logger.IsSome then 
            logger.Value.LogDebug( "RabbitMessaging::Disposing" )
    
        this.Stop() 
        channel.Dispose()
        connection.Dispose()
    
    member this.Setup () =
        // ToAll 
        if logger.IsSome then 
            logger.Value.LogInformation( "RabbitMessaging::Setup - Exchange Declare (ToAll) {Exchange}", options.ToAll.Exchange )
            
        channel.ExchangeDeclare( options.ToAll.Exchange, "fanout" )
        
        // ToOne
        if logger.IsSome then 
            logger.Value.LogInformation( "RabbitMessaging::Setup - Exchange Declare (ToOne) {Exchange}", options.ToOne.Exchange )
        
        channel.ExchangeDeclare( options.ToOne.Exchange, "direct" )
        
        if logger.IsSome then 
            logger.Value.LogInformation( "RabbitMessaging::Setup - CreateRecipient - Exchange Declare (ToAny) {Exchange}", options.ToAny.Exchange )

        channel.ExchangeDeclare( options.ToAny.Exchange, "direct" )

        //if logger.IsSome then 
        //    logger.Value.LogInformation( "CreateRecipient - Queue Declare (ToAny) {Queue}", options.ToAny.Queue.Value )
            
        //channel.QueueDeclare( queue = options.ToAny.Queue.Value, durable = false, exclusive = false ) |> ignore

        
    member this.Start () = 
        this.Setup()
        seq { 1 .. options.NumberOfOutboxThreads }
        |> Seq.iter ( fun idx ->
            let wt = new System.Threading.Thread( new System.Threading.ThreadStart( this.ProcessOutbox ) )
            wt.Name <- sprintf "RabbitMessagingOutboxThread(%d)" idx
            wt.Start() )
        
    member this.Stop () =

        if logger.IsSome then 
            logger.Value.LogDebug( "RabbitMessaging::Stopping" )
            
        outbox.CompleteAdding()            
    
    member this.AddRecipient (recipient:IRecipient) =   
        ()
                
    member this.RemoveRecipient (recipientId:RecipientId) = 
        false
        
    member this.ProcessOutbox () = 
    
        if logger.IsSome then 
            logger.Value.LogDebug( "RabbitMessaging::ProcessOutbox - Thread starting {ThreadName}", System.Threading.Thread.CurrentThread.Name )
    
        try 
            while not outbox.IsCompleted do
            
                let envelope = 
                    try 
                        let envelope = 
                            outbox.Take()
                            
                        if logger.IsSome then 
                            logger.Value.LogDebug( "RabbitMessaging::ProcessOutbox - Subject={Subject} Recipients={Recipients}", envelope.Message.Header.Subject, envelope.Recipients )
                            
                        Some envelope                            
                        
                    with 
                    | :? System.InvalidOperationException as ioe ->
                        None
                    | _ -> 
                        reraise()
                        
                match envelope with 
                | Some e -> 
                    try
                        this.Send( e )
                    finally
                        ()
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
            logger.Value.LogDebug( "RabbitMessaging::ProcessOutbox - Thread finishing {ThreadName}", System.Threading.Thread.CurrentThread.Name )
                     
    member this.CreateRecipient (label:string) (description:string) =
    
        let recipientId = 
            RecipientId.Make( System.Guid.NewGuid().ToString("N"), description )
            
        if logger.IsSome then 
            logger.Value.LogTrace( "RabbitMessaging:CreateRecipient - RecipientId {RecipientId} Label {Label}", recipientId, label )
            
        let recipientChannel =
            //channel 
            connection.CreateModel()
            
        let options = 
            RabbitRecipientOptions.Make( 
                options.Logger, 
                label,
                recipientId,
                this,
                recipientChannel,
                options.ToAll,
                options.ToOne,
                options.ToAny,
                options.SelfDeliver)
                
        RabbitRecipient.Make( serialiser, options )    
                                                                                                
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
         
