namespace Example.Messaging.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Serialisation 

open Example.Messaging
open Example.Messaging.Rabbit
             
type ImplementationCreator = {
    Name : string 
    Creator : ISerde -> ILogger -> IMessaging
}
with 
    static member Make( name, creator ) = 
        { Name = name; Creator = creator }

    override this.ToString() = this.Name 
                      
type MessagingShould( oh: ITestOutputHelper ) = 

    // with some implementations we can't tell how long / when a rely might 
    // back to us so before we 'stop' the messaging implementation and make assertions
    // we need to wait a 'short while' to let any expected messages get back to us
    let stopWaitMilliseconds = 
        1000
        
    let logger =
    
        let options = 
            { Logging.Options.Default 
                with 
                    OutputHelper = Some oh
                    Level = LogLevel.Trace }
        
        Logging.CreateLogger options
    
    let serialiser = 
        Helpers.DefaultSerde 
        
    static member Memory (serialiser:ISerde) (logger:ILogger) = 
    
        let options = 
            { MemoryMessagingOptions.Default with Logger = Some logger }
            
        MemoryMessaging.Make( serialiser, options ) 

    static member Rabbit (serialiser:ISerde) (logger:ILogger) = 
    
        let options = 
            { RabbitMessagingOptions.Default with Logger = Some logger }
            
        RabbitMessaging.Make( serialiser, options ) 

    static member Implementations
    
        with get () = 

            seq { 
                yield [| ImplementationCreator.Make( "memory", MessagingShould.Memory ) |]
                //yield [| ImplementationCreator.Make( "rabbit", MessagingShould.Rabbit ) |]  
            }
        
    [<Theory>]
    [<MemberData("Implementations")>]
    member this.``ToAll-SameLabel`` (v:ImplementationCreator) = 
    
        use sut = v.Creator serialiser logger
        
        sut.Start()

        // setup sender
        
        use broadcaster =
            sut.CreateRecipient "worker" "broadcaster" 

        broadcaster.AddReceiver <| Receiver.Make( "broadcaster", None, (fun msg -> None) ) |> ignore
        
        
        // setup receivers 
        
        use receiver1 = 
            sut.CreateRecipient "worker" "receiver1"
            
        receiver1.AddReceiver <| Receiver.Make( "receiver1", None, (fun msg -> None) ) |> ignore 

        use receiver2 = 
            sut.CreateRecipient "worker" "receiver2"
            
        receiver2.AddReceiver <| Receiver.Make( "receiver2", None, (fun msg -> None) ) |> ignore 
        
        let message =
                        
            let header = 
                Header.Make( None, None )
                                
            Message.Make( header, Body.Content( Mocks.Person.Example() ) )
                                                
        broadcaster.Send (Recipients.ToAll(None), message ) 
        
        // stop will wait till all messages are complete
        System.Threading.Thread.Sleep( stopWaitMilliseconds )
        sut.Stop()
        
        Assert.Equal( 1, broadcaster.Statistics.Sent )
        Assert.Equal( 0, broadcaster.Statistics.Received )
                    
        Assert.Equal( 0, receiver1.Statistics.Sent )
        Assert.Equal( 1, receiver1.Statistics.Received )
           
        Assert.Equal( 0, receiver2.Statistics.Sent )
        Assert.Equal( 1, receiver2.Statistics.Received )   
        
        
    [<Theory>]
    [<MemberData("Implementations")>]
    member this.``ToOne-NoReply`` (v:ImplementationCreator) = 
    
        use sut = v.Creator serialiser logger
    
        sut.Start()
        
        // setup sender
        
        use sender =
            sut.CreateRecipient "worker" "sender"

        sender.AddReceiver <| Receiver.Make( "sender", None, (fun msg -> None) ) |> ignore
        
        // setup receiver 
        
        use receiver = 
            sut.CreateRecipient "worker" "receiver"
            
        let receiverOnReply (msg:IMessage) =
            None
            
        receiver.AddReceiver <| Receiver.Make( "receiver", None, receiverOnReply ) |> ignore 
        
        // setup another receiver 
        
        use receiverOther = 
            sut.CreateRecipient "worker" "otherReceiver"
            
        receiverOther.AddReceiver <| Receiver.Make( "receiverOther", None, receiverOnReply ) |> ignore 

        let message = 
    
            let header = 
                Header.Make( None, Some sender.RecipientId )
                            
            Message.Make( header, Body.Content( Mocks.Person.Example() ) )
                                                
        sender.Send ( Recipients.ToOne( receiver.RecipientId ), message ) 
        
        // stop will wait till all messages are complete
        System.Threading.Thread.Sleep( stopWaitMilliseconds )
        sut.Stop()
        
        Assert.Equal( 1, sender.Statistics.Sent )
        Assert.Equal( 0, sender.Statistics.Received )     
               
        Assert.Equal( 0, receiver.Statistics.Sent )
        Assert.Equal( 1, receiver.Statistics.Received )

        Assert.Equal( 0, receiverOther.Statistics.Sent )
        Assert.Equal( 0, receiverOther.Statistics.Received )
                     
                
    [<Theory>]
    [<MemberData("Implementations")>]
    member this.``ToOne-WithReply`` (v:ImplementationCreator) = 
    
        use sut = v.Creator serialiser logger
    
        sut.Start()
        
        // setup sender
        
        use sender =
            sut.CreateRecipient "worker" "sender"

        sender.AddReceiver <| Receiver.Make( "sender-receiver", None, (fun msg -> None) ) |> ignore
        
        // setup receiver 
        
        use receiver = 
            sut.CreateRecipient "worker" "receiver"
            
        let receiverOnReply (msg:IMessage) =
        
            let replyTo= 
                msg.Header.ReplyTo 

            if replyTo.IsSome then
                             
                let replyMessage = 
                    Message.Make( Body.Content( Mocks.Empty.Example() ) )
                        
                    
                Some <| ( Recipients.ToOne( replyTo.Value ), replyMessage )
            else 
                None 
            
        receiver.AddReceiver <| Receiver.Make( "receiver-receiver", None, receiverOnReply ) |> ignore 
        
        let message = 
    
            let header = 
                Header.Make( None, Some sender.RecipientId )
                            
            Message.Make( header, Body.Content( Mocks.Person.Example() ) )
                                                
        sender.Send ( Recipients.ToOne( receiver.RecipientId ), message ) 
        
        // stop will wait till all messages are complete
        System.Threading.Thread.Sleep( stopWaitMilliseconds )
        sut.Stop()
        
        Assert.Equal( 1, sender.Statistics.Sent )
        Assert.Equal( 1, sender.Statistics.Received )     
               
        Assert.Equal( 1, receiver.Statistics.Sent )
        Assert.Equal( 1, receiver.Statistics.Received )            
    
    
    [<Theory>]
    [<MemberData("Implementations")>]
    member this.``ToAny-WithReply`` (v:ImplementationCreator) = 
    
        use messaging = 
            v.Creator serialiser logger 

        messaging.Start()            
        
        // setup sender
        
        use sender =
            messaging.CreateRecipient "client" "sender"

        sender.AddReceiver <| Receiver.Make( "sender", None, (fun msg -> None) ) |> ignore
        

        let receiverOnReply (msg:IMessage) =
        
            logger.LogInformation( "receiverOnReply {Message}", msg )
            
            let replyTo= 
                msg.Header.ReplyTo 

            if replyTo.IsSome then
                             
                let replyMessage = 
                    Message.Make( Body.Content( Mocks.Empty.Example() ) )
                        
                    
                Some <| ( Recipients.ToOne( replyTo.Value ), replyMessage )
            else 
                None 
            
        let nReceivers = 3
        
        let receivers = 
            seq { 0 .. nReceivers-1 }
            |> Seq.map ( fun idx ->
            
                let recipient = 
                    messaging.CreateRecipient "server" (sprintf "recipient%d" idx)
                    
                recipient.AddReceiver <| Receiver.Make( sprintf "receiver%d" idx, None, receiverOnReply ) 
                
                recipient )
            |> Array.ofSeq 
        

        let message = 
    
            let header = 
                Header.Make( None, Some sender.RecipientId )
                            
            Message.Make( header, Body.Content( Mocks.Person.Example() ) )

        let nMessages = 2
        
        seq { 0 .. nMessages-1 }
        |> Seq.iter ( fun _ ->                                                
            sender.Send ( Recipients.ToAny( Some "server" ), message ) ) 
        
        // stop will wait till all messages are complete
        System.Threading.Thread.Sleep( stopWaitMilliseconds )
        messaging.Stop()
        
        let totalReceiversReceived = 
            receivers |> Seq.map ( fun r -> r.Statistics.Received ) |> Seq.sum 
        
        Assert.Equal( nMessages, totalReceiversReceived )


        let totalReceiversSent = 
            receivers |> Seq.map ( fun r -> r.Statistics.Sent ) |> Seq.sum 

        Assert.Equal( nMessages, totalReceiversSent )
        

        Assert.Equal( nMessages, sender.Statistics.Sent )
        Assert.Equal( nMessages, sender.Statistics.Received )     
               
