namespace Example.Messaging.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Messaging
                                            
type MessageShould( oh: ITestOutputHelper ) = 

    [<Fact>]
    member this.``CreateAndExposeCorrectProperties`` () = 
    
        let header = 
            Header.Make( None, None )
                        
        let sut = 
            Message.Make( header, Body.Content( Mocks.Person.Example() ) ) 
         
        Assert.Equal( None, sut.Header.Subject )
        Assert.Equal( None, sut.Header.ReplyTo )
        Assert.Equal( header, sut.Header )
        
        
            
     