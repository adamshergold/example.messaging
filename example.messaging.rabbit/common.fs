namespace Example.Messaging.Rabbit

open Microsoft.Extensions.Logging 

open RabbitMQ.Client
open RabbitMQ.Client.Exceptions

open Polly 

[<AutoOpen>]
module CommonImpl =
    
    let TryEnvironmentVariable (name:string) =
        
        let ev =
            System.Environment.GetEnvironmentVariable(name)
            
        if ev <> null && ev.Trim().Length > 0 then
            Some ev
        else 
            None 

    
module Common = 

    [<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
    type FactoryOptions = {
        Logger : ILogger option
        HostName : string 
        RetryCount : int
        RetryBackOffSeconds : int[]
        UserName : string
        Password : string
    }
    with 
        static member Default = {
        
            Logger = 
                None
                
            HostName =
                TryEnvironmentVariable "EXAMPLE_RABBIT_HOSTNAME" 
                |> Option.defaultValue "localhost"
                
            RetryCount = 10    
            
            RetryBackOffSeconds = [| 1; 2; 4; 8; 16; 32; 64 |]
            
            UserName =
                TryEnvironmentVariable "EXAMPLE_RABBIT_USERNAME" 
                |> Option.defaultValue "guest"

            Password =
                TryEnvironmentVariable "EXAMPLE_RABBIT_PASSWORD" 
                |> Option.defaultValue "guest"
                        
        }
        
    [<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]        
    type Factory( options: FactoryOptions ) =
    
        let factory = 
            let f = new ConnectionFactory()
            f.HostName <- options.HostName
            f.UserName <- options.UserName
            f.Password <- options.Password
            f
        
        member val Options = options 
        
        member val Factory = factory 
        
        member this.CreateConnection () = 
            this.Factory.CreateConnection() 
            
        static member Make( options ) =
            new Factory( options ) 
            
    [<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]            
    type ConnectionManager( logger:ILogger option, factory: Factory ) =
    
        let endPoint = 
            factory.Factory.Endpoint.ToString() 
    
        let tryConnection () =
                
            let onRetry (ex:System.Exception) retry =
                if logger.IsSome then 
                    logger.Value.LogWarning( "Connection::tryConnection({EndPoint}) - Attempt failed: {Message} (will retry in {Retry} seconds)", endPoint, ex.Message, retry )
                
            let action () =
                if logger.IsSome then 
                    logger.Value.LogInformation( "Connection::tryConnection({EndPoint}) - Connecting ...", endPoint )
                    
                Some <| factory.CreateConnection()
    
            let retryDurationProvider (retryAttempt:int) = 
            
                let delay = 
                    if retryAttempt < factory.Options.RetryBackOffSeconds.Length then
                        factory.Options.RetryBackOffSeconds.[retryAttempt]
                    else
                        factory.Options.RetryBackOffSeconds.[factory.Options.RetryBackOffSeconds.Length-1] 
                
                System.TimeSpan.FromSeconds( (float)delay )
                
            let retry =                
                Policy
                    .Handle<BrokerUnreachableException>()
                    .WaitAndRetry(
                        retryCount = factory.Options.RetryCount,
                        sleepDurationProvider = retryDurationProvider,
                        onRetry = onRetry )
    
            let fallback =
            
                let fallbackAction () =
                    if logger.IsSome then 
                        logger.Value.LogError( "Connection::tryConnection({EndPoint}) - Could not establish connection!", endPoint )
                         
                    None
                     
                Policy<IConnection option>.Handle<BrokerUnreachableException>().Fallback(fallbackAction = fallbackAction)
               
            let policy = 
                fallback.Wrap(retry)
                            
            policy.Execute( fun () -> action() )             
                            
        let mutable connection : IConnection option = None 
        
        static member Make( host ) = 
            new ConnectionManager( host ) 
            
        member this.Dispose () =
        
            if connection.IsSome then 
                connection.Value.Close()
                connection.Value.Dispose()            
    
        member this.OnConnectionShutdown eventArgs = 
            if logger.IsSome then 
                logger.Value.LogWarning( "Connection::OnConnectionShutdown({EndPoint}) - Received ConnectionShutdown event from RabbitMQ", endPoint )
            ()
            
        member this.Shared 
            with get () = 
                lock this ( fun () ->
                
                    if connection.IsNone then 
                        connection <- tryConnection()
                
                    if connection.IsNone then
                        if logger.IsSome then  
                            logger.Value.LogError( "Could not establish RabbitMQ connection!" )
                        failwithf "Could not establish RabbitMQ connection!" 
                    else   
                        connection.Value.ConnectionShutdown.Add( this.OnConnectionShutdown )
                        connection.Value )    

        member this.CreateConnection () = 
            match tryConnection() with 
            | Some c -> c
            | None -> failwithf "Unable to create RabbitConnection!"
                            
        interface System.IDisposable
            with 
                member this.Dispose () = 
                    this.Dispose()
                    
    [<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
    type RabbitEndPoint = {
        Exchange : string
        Queue : string option
    }
    with 
        static member Make( exch, queue ) = 
            { Exchange = exch; Queue = queue }                    