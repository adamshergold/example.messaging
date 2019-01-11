namespace Example.Messaging.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Messaging
                                            
type RecipientsShould( oh: ITestOutputHelper ) = 

    [<Fact>]
    member this.``CanSerialise-ToAll`` () = 
    
        let serialiser = 
            Helpers.DefaultSerde 
            
        let sut = 
            Recipients.ToAll(None)
                    
        let rt = Helpers.RoundTrip serialiser "binary" sut
        
        Assert.Equal( rt, sut )
                     

    [<Fact>]
    member this.``CanSerialise-ToAny`` () = 
    
        let serialiser = 
            Helpers.DefaultSerde 
            
        let sut = 
            Recipients.ToAny( Some "label" )
                    
        let rt = Helpers.RoundTrip serialiser "binary" sut 
     
        Assert.Equal( rt, sut )
        
    [<Fact>]
    member this.``CanSerialise-ToOne`` () = 
    
        let serialiser = 
            Helpers.DefaultSerde 
            
        let sut = 
            Recipients.ToOne( RecipientId.Make( "123") )
                    
        let rt = Helpers.RoundTrip serialiser "binary" sut 
        
        Assert.Equal( rt, sut )