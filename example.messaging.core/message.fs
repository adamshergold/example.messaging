namespace Example.Messaging

open Example.Serialisation
open Example.Serialisation.Binary

type Message( correlationId: string option, header: IHeader, body: Body ) = 

    member val CorrelationId = correlationId 
    
    member val Header = header 
    
    member val Body = body 

    override this.ToString() = 
        sprintf 
            "Message(CorrelationId=%s,Header=%s,Body=%s)"
            (match this.CorrelationId with | Some v -> v | None -> "-")
            (header.ToString())
            (body.ToString())
        
    static member Make( body ) = 
        new Message( None, Header.Make( None, None ), body ) :> IMessage
    
    static member Make( correlationId, header, body ) = 
        new Message( correlationId, header, body ) :> IMessage

    static member Make( header, body ) = 
        new Message( None, header, body ) :> IMessage
    
    interface ITypeSerialisable
          
    override this.GetHashCode () =
        hash (this.CorrelationId, this.Header, this.Body )
        
    override this.Equals (other:obj) =
        match other with
        | :? Message as other ->
            other.CorrelationId= this.CorrelationId && other.Header = this.Header  && other.Body.Equals( this.Body )
        | _ ->
            failwithf "Cannot check equality between Header and '%O'" (other.GetType())
                    
    static member Serialiser 
        with get () =   
            { new ITypeSerde<Message> 
                with 
                    member this.TypeName =
                        "__message"
                        
                    member this.ContentType = 
                        "binary" 
                                                    
                    member this.Serialise (serialiser:ISerde) (s:ISerdeStream) (v:Message) =
                    
                        use bs = 
                            BinarySerialiser.Make( serialiser, s, this.TypeName )
                            
                        bs.Write(v.CorrelationId.IsSome)
                        if v.CorrelationId.IsSome then 
                            bs.Write(v.CorrelationId.Value)
                                
                        bs.Write(v.Header)
                        
                        bs.Write(v.Body)
    
                    member this.Deserialise (serialiser:ISerde) (s:ISerdeStream) =
                    
                        use bds =
                            BinaryDeserialiser.Make( serialiser, s, this.TypeName )

                        let correlationId = 
                            if bds.ReadBool() then 
                                Some <| bds.ReadString()
                            else 
                                None
                                    
                        let header = 
                            bds.ReadRecord<_>()
                            
                        let body = 
                            bds.ReadUnion<_>()
                                                                            
                        new Message( correlationId, header, body ) }         
                            
    interface IMessage
        with 
            member this.CorrelationId = this.CorrelationId 
            
            member this.Header = this.Header 
            
            member this.Body = this.Body 
            
