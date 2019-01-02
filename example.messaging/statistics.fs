namespace Example.Messaging

type Statistics( sent: int, received: int ) =

    member val Sent = sent

    member val Received = received

    static member Make( sent, received ) =
        new Statistics( sent, received ) :> IStatistics
    
    interface IStatistics
        with 
            member this.Sent = this.Sent
            
            member this.Received = this.Received
