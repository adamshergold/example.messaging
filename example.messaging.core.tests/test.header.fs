namespace Example.Messaging.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Messaging
                                            
type HeaderShould( oh: ITestOutputHelper ) = 

    [<Fact>]
    member this.``CreateAndExposeCorrectProperties`` () = 
    
        let subject, replyTo = 
            "theSubject", RecipientId.Make( "toMe" ) 
            
        let sut = 
            Header.Make( Some subject, Some replyTo )
         
        Assert.Equal( subject, sut.Subject.Value )
        Assert.Equal( replyTo, sut.ReplyTo.Value )
        
    [<Fact>]
    member this.``CanSerialise`` () = 
    
        let serialiser = 
            Helpers.DefaultSerde 
            
        let subject, replyTo = 
            "theSubject", RecipientId.Make( "toMe" ) 
            
        let sut = 
            Header.Make( Some subject, Some replyTo )
            
        let rt = Helpers.RoundTrip serialiser "binary" sut
        
        Assert.Equal( rt, sut )
         
                    
     