namespace Example.Messaging 

open Example.Serialisation
open Example.Serialisation.Binary

type RecipientId = {
    Id : string
    Description : string option 
}
with 
    static member Make( id ) = 
        { Id = id; Description = None }

    static member Make( id, description ) = 
        { Id = id; Description = Some description }
   
    override this.ToString() = 
        sprintf "Recipient(%s,%s)" this.Id (match this.Description with | Some v -> v | None -> "-")
        
    interface ITypeSerialisable
             
    static member Serialiser 
        with get () =   
            { new ITypeSerde<RecipientId> 
                with 
                    member this.TypeName =
                        "__recipient"

                    member this.ContentType = 
                        "binary" 
                                                    
                    member this.Serialise (serialiser:ISerde) (s:ISerdeStream) (v:RecipientId) =
                    
                        use bs = 
                            BinarySerialiser.Make( serialiser, s, this.TypeName )
                            
                        bs.Write( v.Id )
                                      
                        bs.Write( v.Description.IsSome )
                        if v.Description.IsSome then
                            bs.Write( v.Description.Value)
                            
    
                    member this.Deserialise (serialiser:ISerde) (s:ISerdeStream) =
                    
                        use bds =
                            BinaryDeserialiser.Make( serialiser, s, this.TypeName )

                        let id = 
                            bds.ReadString()
                            
                        let description =
                            if bds.ReadBool() then Some( bds.ReadString() ) else None
                            
                        { Id = id; Description = description } }           
         

type Recipients = 
    | ToAll of string option 
    | ToAny of string option
    | ToOne of RecipientId

    interface ITypeSerialisable
                    
    static member Serialiser 
        with get () =   
            { new ITypeSerde<Recipients> 
                with 
                    member this.TypeName =
                        "__recipients"
                        
                    member this.ContentType = 
                        "binary" 
                                                    
                    member this.Serialise (serialiser:ISerde) (s:ISerdeStream) (v:Recipients) =
                    
                        use bs = 
                            BinarySerialiser.Make( serialiser, s, this.TypeName )
                            
                        match v with 
                        | Recipients.ToAll(label) ->
                            bs.Write( "ToAll" )
                            bs.Write(label.IsSome)
                            if label.IsSome then 
                                bs.Write(label.Value) 
                        | Recipients.ToAny(label) ->
                            bs.Write( "ToAny" )
                            bs.Write(label.IsSome)
                            if label.IsSome then 
                                bs.Write(label.Value)
                        | Recipients.ToOne(recipient)->
                            bs.Write( "ToOne" )
                            bs.Write( recipient )
    
                    member this.Deserialise (serialiser:ISerde) (s:ISerdeStream) =
                    
                        use bds =
                            BinaryDeserialiser.Make( serialiser, s, this.TypeName )

                        match bds.ReadString() with 
                        | _ as v when v = "ToAll"  ->
                             
                            let label =
                                if bds.ReadBool() then Some( bds.ReadString() ) else None 
                                
                            Recipients.ToAll( label )
                            
                        | _ as v when v = "ToAny" ->
                        
                            let label =
                                if bds.ReadBool() then Some( bds.ReadString() ) else None 
                        
                            Recipients.ToAny( label )
                            
                        | _ as v when v = "ToOne" ->
                            Recipients.ToOne( bds.ReadRecord<_>() )
                            
                        | _ as v ->
                            failwithf "Unexpected case seen when deserialising recipients '%s'" v }    
            
         
    