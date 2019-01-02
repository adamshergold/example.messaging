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
        with 
            member this.Type 
                with get () = typeof<Header> 
                    
    static member Serialiser 
        with get () =   
            { new ITypeSerialiser<Header> 
                with 
                    member this.TypeName =
                        "__header"

                    member this.Type 
                        with get () = typeof<Header> 
                        
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
            
