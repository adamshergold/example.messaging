namespace Example.Messaging

open Example.Serialisation
open Example.Serialisation.Binary

type Header( subject: string option, replyTo: RecipientId option ) = 

    member val Subject = subject 
    
    member val ReplyTo = replyTo 
    
    override this.ToString() = 
        sprintf "Header(Subject=%s ReplyTo=%s)" 
            (match subject with | Some v -> v | None -> "-" )
            (match replyTo with | Some v -> v.Id | None -> "-" )  
            
    static member Make( subject, replyTo ) = 
        new Header( subject, replyTo ) :> IHeader
    
    interface ITypeSerialisable
                
    override this.GetHashCode () =
        hash (this.Subject, this.ReplyTo )
        
    override this.Equals (other:obj) =
        match other with
        | :? Header as other ->
            other.Subject.Equals( this.Subject ) && other.ReplyTo.Equals( this.ReplyTo )
        | _ ->
            failwithf "Cannot check equality between Header and '%O'" (other.GetType())
                    
    static member Serialiser 
        with get () =   
            { new ITypeSerde<Header> 
                with 
                    member this.TypeName =
                        "__header"
                        
                    member this.ContentType = 
                        "binary" 
                                                    
                    member this.Serialise (serialiser:ISerde) (s:ISerdeStream) (v:Header) =
                    
                        use bs = 
                            BinarySerialiser.Make( serialiser, s, this.TypeName )
                            
                        bs.Write(v.Subject.IsSome)
                        if v.Subject.IsSome then 
                            bs.Write(v.Subject.Value)
                            
                        bs.Write(v.ReplyTo.IsSome)
                        if v.ReplyTo.IsSome then 
                            bs.Write(v.ReplyTo.Value)    
    
                    member this.Deserialise (serialiser:ISerde) (s:ISerdeStream) =
                    
                        use bds =
                            BinaryDeserialiser.Make( serialiser, s, this.TypeName )

                        let subject = 
                            if bds.ReadBool() then Some <| bds.ReadString() else None
                            
                        let replyTo = 
                            if bds.ReadBool() then Some <| bds.ReadRecord<_>() else None
                        
                        new Header( subject, replyTo ) }         
    interface IHeader
        with 
            member this.Subject = this.Subject 
            
            member this.ReplyTo = this.ReplyTo 
            
