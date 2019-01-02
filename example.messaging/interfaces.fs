namespace Example.Messaging 

open Example.Serialisation

type IHeader = 
    inherit ITypeSerialisable
    abstract Subject : string option with get
    abstract ReplyTo : RecipientId option with get 

type IMessage =
    inherit ITypeSerialisable 
    abstract CorrelationId : string option
    abstract Header : IHeader with get
    abstract Body : Body with get  
        
type IEnvelope =
    inherit ITypeSerialisable 
    abstract Sender : RecipientId with get
    abstract Recipients : Recipients with get
    abstract Message : IMessage with get
    
type IStatistics =  
    abstract Sent : int with get
    abstract Received : int with get

type ReceiverId = string 

type Receiver = {
    ReceiverId : ReceiverId
    Subject : string option
    OnHandler : IMessage -> Async<(Recipients*IMessage) option>
}
with
    static member Make( id, subject, handler ) = 
        { ReceiverId = id; Subject = subject; OnHandler = handler }    
 
    static member Make( subject, handler ) = 
        { ReceiverId = System.Guid.NewGuid().ToString("N"); Subject = subject; OnHandler = handler }    

type IRecipient = 
    inherit System.IDisposable 
    abstract RecipientId : RecipientId with get
    abstract Label : string with get
    abstract Deliver : IMessage -> unit
    abstract AddReceiver : Receiver -> unit
    abstract RemoveReceiver : ReceiverId -> bool
    abstract Receivers : seq<Receiver> with get
    abstract Send : Recipients * IMessage -> unit
    abstract Statistics : IStatistics with get 
    
type IMessaging =
    inherit System.IDisposable 
    abstract Send : IEnvelope -> unit
    abstract CreateRecipient : label:string -> description:string -> IRecipient
    abstract AddRecipient : IRecipient -> unit
    abstract RemoveRecipient : RecipientId -> bool
    abstract Start : unit -> unit 
    abstract Stop : unit -> unit         
         
    