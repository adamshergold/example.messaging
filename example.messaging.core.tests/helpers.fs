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
                
    let RoundTrip (serde:ISerde) (v:ITypeSerialisable) = 
    
        let bytes = 
            Example.Serialisation.Helpers.Serialise serde None v 
        
        let typeName = 
            serde.TypeName None v.Type 
            
        Example.Serialisation.Helpers.Deserialise serde None typeName bytes