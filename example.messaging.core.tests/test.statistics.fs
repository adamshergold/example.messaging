namespace Example.Messaging.Tests

open Microsoft.Extensions.Logging 

open Xunit
open Xunit.Abstractions 

open Example.Messaging
                                            
type StatisticsShould( oh: ITestOutputHelper ) = 

    [<Fact>]
    member this.``CreateAndExposeCorrectProperties`` () = 
    
        let nSent, nReceived =  
            1, 0 
            
        let sut = 
            Statistics.Make( nSent, nReceived ) 
         
        Assert.Equal( nSent, sut.Sent )
        Assert.Equal( nReceived, sut.Received )
        
            
     