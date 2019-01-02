namespace Example.Messaging.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Messaging
                                            
type ErrorShould( oh: ITestOutputHelper ) = 

    let logger =
    
        let options = 
            { Logging.Options.Default with OutputHelper = Some oh }
        
        Logging.CreateLogger options
    
    [<Fact>]
    member this.``CreateAndExposeCorrectProperties`` () = 
    
        let message, retryable = 
            "something went wrong", true  
            
        let sut = 
            Error.Make( message, retryable )
         
        Assert.Equal( message, sut.Message )
        Assert.Equal( retryable, sut.Retryable )
        
            
    [<Fact>]
    member this.``CanSerialise`` () = 
    
        let serialiser = 
            Helpers.DefaultSerde 
            
        let sut = Error.Make( "oops!", false ) 
                    
        Helpers.RoundTrip serialiser sut      