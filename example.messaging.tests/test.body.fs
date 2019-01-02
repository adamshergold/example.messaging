namespace Example.Messaging.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Messaging
                                            
type BodyShould( oh: ITestOutputHelper ) = 

    let logger =
    
        let options = 
            { Logging.Options.Default with OutputHelper = Some oh }
        
        Logging.CreateLogger options

    [<Fact>]
    member this.``CanSerialise-Error`` () = 
    
        let serialiser = 
            Helpers.DefaultSerde 
            
        let sut =
            let v = Error.Make( "Foo", true ) 
            Body.Error( v ) 
                    
        Helpers.RoundTrip serialiser sut    
              
    [<Fact>]
    member this.``CanSerialise-Content`` () = 
    
        let serialiser = 
            Helpers.DefaultSerde 
            
        let sut =
            let v = Mocks.Person.Make( "John Smith" ) 
            Body.Content( v ) 
                    
        Helpers.RoundTrip serialiser sut      