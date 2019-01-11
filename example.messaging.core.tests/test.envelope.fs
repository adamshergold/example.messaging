namespace Example.Messaging.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Messaging
                                            
type EnvelopeShould( oh: ITestOutputHelper ) = 

    let sut () = 

        let recipientId = 
            RecipientId.Make( "replyTo" )
            
        let header = 
            Header.Make( Some "theSubject", Some recipientId )
            
        Message.Make( header, Body.Content( Mocks.Person.Example() ) )
    
    [<Fact>]
    member this.``CreateAndExposeCorrectProperties`` () = 
    
        let message =
            sut()        
            
        let recipients = 
            Recipients.ToAll(None)
                    
        let sut = 
            
            Envelope.Make( RecipientId.Make( "sender1" ), recipients, message )
         
        Assert.Equal( RecipientId.Make( "sender1" ), sut.Sender ) 
        Assert.Equal( "theSubject", sut.Message.Header.Subject.Value )
        Assert.Equal( Recipients.ToAll(None), sut.Recipients )
        
    [<Theory>]
    [<InlineData("binary")>]
    member this.``CanSerialise`` (contentType:string) = 
    
        let serialiser = 
            Helpers.DefaultSerde 
            
        let sut = sut() 
                    
        let rt = Helpers.RoundTrip serialiser contentType sut
        
        Assert.Equal( rt, sut )
                     
     