namespace Example.Messaging

open Example.Serialisation
open Example.Serialisation.Binary

type Envelope( sender: RecipientId, recipients: Recipients, message:IMessage ) =  

    member val Sender = sender 
    
    member val Recipients = recipients 

    member val Message = message 

    static member Make( sender, recipients, message ) = 
        new Envelope( sender, recipients, message ) :> IEnvelope
    
    interface ITypeSerialisable

    override this.GetHashCode () =
        hash (this.Sender, this.Recipients, this.Message )
        
    override this.Equals (other:obj) =
        match other with
        | :? Envelope as other ->
            other.Sender.Equals( this.Sender ) && other.Recipients.Equals( this.Recipients ) && other.Message.Equals( this.Message )
        | _ ->
            failwithf "Cannot check equality between Envelope and '%O'" (other.GetType())
                        
    static member Serialiser 
        with get () =   
            { new ITypeSerde<Envelope> 
                with 
                    member this.TypeName =
                        "__envelope"

                    member this.ContentType = 
                        "binary" 
                                                    
                    member this.Serialise (serialiser:ISerde) (s:ISerdeStream) (v:Envelope) =
                    
                        use bs = 
                            BinarySerialiser.Make( serialiser, s, this.TypeName )
                            
                        bs.Write( v.Sender )
                                                    
                        bs.Write( v.Recipients )
                        
                        bs.Write( v.Message )
                            
    
                    member this.Deserialise (serialiser:ISerde) (s:ISerdeStream) =
                    
                        use bds =
                            BinaryDeserialiser.Make( serialiser, s, this.TypeName )

                        let sender = 
                            bds.ReadRecord<_>()
                            
                        let recipients =
                            bds.ReadUnion<_>()
                            
                        let message = 
                            bds.ReadInterface<_>()
                            
                        new Envelope( sender, recipients, message ) }                    
        
    interface IEnvelope
        with 
            member this.Sender = this.Sender
            
            member this.Recipients = this.Recipients
            
            member this.Message = this.Message            
    
    