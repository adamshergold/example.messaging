namespace Example.Messaging 

open Example.Serialisation
open Example.Serialisation.Binary

type Body = 
    | Content of ITypeSerialisable 
    | Error of IError 

    override this.ToString() = 
        match this with 
        | Content(ts) -> sprintf "Body(Content(%O))" (ts.GetType())
        | Error(e) -> sprintf "Body(Error(%s))" (e.ToString())
        
    interface ITypeSerialisable
    
    static member Serialiser 
        with get () =   
            { new ITypeSerde<Body> 
                with 
                    member this.TypeName =
                        "__body"

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

                            bs.Write( tw.ContentType )
                                
                            bs.Write( tw.TypeName.IsSome )
                            if tw.TypeName.IsSome then
                                bs.Write( tw.TypeName.Value )
                            
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
                                bds.ReadString() 
                                 
                            let typeName = 
                                if bds.ReadBool() then Some( bds.ReadString() ) else None
                                
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