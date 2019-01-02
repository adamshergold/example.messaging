namespace Example.Messaging

open Example.Serialisation
open Example.Serialisation.Binary

type IError = 
    inherit ITypeSerialisable
    abstract Message : string with get 
    abstract Retryable : bool with get


type Error( message:string, retryable: bool ) = 

    member val Message = message 
    
    member val Retryable = retryable
    
    static member Make( message, retryable ) = 
        new Error( message, retryable ) :> IError
     
    override this.ToString() = 
        sprintf "Message=%s Retryable=%b" message retryable
             
    interface ITypeSerialisable
        with 
            member this.Type 
                with get () = typeof<Error> 
        
    static member Serialiser 
        with get () =   
            { new ITypeSerialiser<Error> 
                with 
                    member this.TypeName =
                        "__error"

                    member this.Type 
                        with get () = typeof<Error> 
                        
                    member this.ContentType = 
                        "binary" 
                                                    
                    member this.Serialise (serialiser:ISerde) (s:ISerdeStream) (v:Error) =
                    
                        use bs = 
                            BinarySerialiser.Make( serialiser, s, this.TypeName )
                            
                        bs.Write( v.Message ) 
                        bs.Write( v.Retryable )                            

    
                    member this.Deserialise (serialiser:ISerde) (s:ISerdeStream) =
                    
                        use bds =
                            BinaryDeserialiser.Make( serialiser, s, this.TypeName )

                        let message = 
                            bds.ReadString()
                          
                        let retryable = 
                            bds.ReadBool()
                            
                        new Error( message, retryable ) }                              
            
    interface IError 
        with 
            member this.Message = this.Message 
            
            member this.Retryable = this.Retryable 
                    