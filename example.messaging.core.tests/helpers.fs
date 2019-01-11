namespace Example.Messaging.Tests 

open Example.Serialisation 
open Example.Serialisation.Json
open Example.Serialisation.Binary

open Example.Messaging 

module Helpers = 
    
    let Serde () =
    
        let options =   
            SerdeOptions.Default
         
        let serde = 
            Serde.Make( options )
            
        serde.TryRegisterAssembly typeof<Envelope>.Assembly |> ignore
        serde.TryRegisterAssembly typeof<BinaryProxy>.Assembly |> ignore
        serde.TryRegisterAssembly typeof<JsonProxy>.Assembly |> ignore
        serde.TryRegisterAssembly typeof<Mocks.Person>.Assembly |> ignore 
        
        serde                 
        
    let DefaultSerde = 
        Serde() 
                
    let RoundTrip (serde:ISerde) (contentType:string) (v:ITypeSerialisable) = 
    
        let bytes = 
            Example.Serialisation.Helpers.Serialise serde contentType v 
        
        let typeSerde =
            serde.TrySerdeBySystemType (contentType,v.GetType())
        
        if typeSerde.IsNone then
            failwithf "Unable to find TypeSerde!"
            
        let typeName =
            typeSerde.Value.TypeName
            
        Example.Serialisation.Helpers.Deserialise serde contentType typeName bytes