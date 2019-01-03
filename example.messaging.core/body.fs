namespace Example.Messaging 

open Example.Serialisation
open Example.Serialisation.Binary

type Body = 
    | Content of ITypeSerialisable 
    | Error of IError 

    override this.ToString() = 
        match this with 
        | Content(ts) -> sprintf "Body(Content(%s))" (ts.Type.Name)
        | Error(e) -> sprintf "Body(Error(%s))" (e.ToString())
        
    interface ITypeSerialisable
        with 
            member this.Type 
                with get () = typeof<Body> 
    
    static member Serialiser 
        with get () =   
            { new ITypeSerialiser<Body> 
                with 
                    member this.TypeName =
                        "__body"

                    member this.Type 
                        with get () = typeof<Body> 
                        
                    member this.ContentType = 
                        "binary" 
                                                    
                    member this.Serialise (serialiser:ISerde) (s:ISerdeStream) (v:Body) =
                    
                        use bs = 
                            BinarySerialiser.Make( serialiser, s, this.TypeName )
                            
                        match v with 
                        | Content(ts) ->
                            bs.Write("Content")
                            
                            let tw = 
                                Helpers.Wrap serialiser ts [ "binary"; "json" ] 

                            bs.Write( tw.ContentType.IsSome )
                            if tw.ContentType.IsSome then 
                                bs.Write( tw.ContentType.Value )
                                
                            bs.Write( tw.TypeName )
                            
                            bs.Write( (int32) tw.Body.Length )
                            bs.Write( tw.Body )
                                  
                        | Error(e) ->
                            bs.Write("Error")
                            bs.Write( e )
    
                    member this.Deserialise (serialiser:ISerde) (s:ISerdeStream) =
                    
                        use bds =
                            BinaryDeserialiser.Make( serialiser, s, this.TypeName )

                        match bds.ReadString() with 
                        | _ as v when v = "Content" ->
                        
                            let contentType = 
                                if bds.ReadBool() then Some( bds.ReadString() ) else None
                                 
                            let typeName = 
                                bds.ReadString()
                                
                            let body = 
                                bds.ReadBytes( bds.ReadInt32() )
                                         
                            let tw = 
                                TypeWrapper.Make( contentType, typeName, body ) 
                                                                         
                            Body.Content( Helpers.Unwrap serialiser tw :?> ITypeSerialisable )
                                                        
                        | _ as v when v = "Error" ->
                            let error = bds.ReadRecord<_>()
                            Body.Error( error )
                             
                        | _ as v ->
                            failwithf "Unexpected case seen when deserialising body '%s'" v }    