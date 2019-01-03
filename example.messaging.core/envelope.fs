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
        with 
            member this.Type 
                with get () = typeof<Envelope> 
                    
    static member Serialiser 
        with get () =   
            { new ITypeSerialiser<Envelope> 
                with 
                    member this.TypeName =
                        "__envelope"

                    member this.Type 
                        with get () = typeof<Envelope> 
                        
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
    
    