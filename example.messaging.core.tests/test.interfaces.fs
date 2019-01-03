namespace Example.Messaging.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Messaging
                                            
type InterfacesShould( oh: ITestOutputHelper ) = 

    let logger =
    
        let options = 
            { Logging.Options.Default with OutputHelper = Some oh }
        
        Logging.CreateLogger options
           

    [<Fact>]
    member this.``CreateReceiver`` () = 
    
        let r1 = 
            Receiver.Make( None, (fun msg -> async { return None } ) )   
            
        Assert.True( r1.Subject.IsNone )
        Assert.True( r1.ReceiverId.Length > 0 )       
        
        let r2 = 
            Receiver.Make( "rid", Some "subject", (fun msg -> async { return None } ) )   
            
        Assert.Equal( "subject", r2.Subject.Value )
        Assert.Equal( "rid", r2.ReceiverId )               