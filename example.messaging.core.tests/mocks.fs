namespace Example.Messaging.Tests

open Example.Serialisation
open Example.Serialisation.Json
open Example.Serialisation.Binary

open Example.Messaging 

module Mocks = 

    type Empty () = 
     
        static member Make() = 
            new Empty() 
            
        static member Example () = 
            Empty.Make() 
            
        interface ITypeSerialisable

        static member JSONSerialiser 
            with get () = 
                { new ITypeSerde<Empty>
                    with
                        member this.TypeName =
                            "Empty"
        
                        member this.ContentType
                            with get () = "json"
        
                        member this.Serialise (serialiser:ISerde) (stream:ISerdeStream) (v:Empty) =
        
                            use js =
                                JsonSerialiser.Make( serialiser, stream, this.ContentType )
        
                            js.WriteStartObject()
                            js.WriteProperty "@type"
                            js.WriteValue this.TypeName
        
                            js.WriteEndObject()
        
                        member this.Deserialise (serialiser:ISerde) (stream:ISerdeStream) =
        
                            use jds =
                                JsonDeserialiser.Make( serialiser, stream, this.ContentType, this.TypeName )
        
                            jds.Deserialise()
        
                            Empty.Make() }
                            
        static member Serialiser 
            with get () =   
                { new ITypeSerde<Empty> 
                    with 
                        member this.TypeName =
                            "Empty"
                
                        member this.ContentType = 
                            "binary" 
                                                       
                        member this.Serialise (serialiser:ISerde) (s:ISerdeStream) (v:Empty) =

                            use bs = 
                                BinarySerialiser.Make( serialiser, s, this.TypeName )
                                
                            ()    
        
                        member this.Deserialise (serialiser:ISerde) (s:ISerdeStream) =
                        
                            use bds = 
                                BinaryDeserialiser.Make( serialiser, s, this.TypeName )

                            Empty.Make() }
          
    type Person = {
        Name : string 
    }
    with 
        static member Make( name ) = 
            { Name = name } 
            
        static member Example () = 
            {
                Name = "John Smith" 
            }
        
        interface ITypeSerialisable

        static member JSONSerialiser 
            with get () = 
                { new ITypeSerde<Person>
                    with
                        member this.TypeName =
                            "Person"
        
                        member this.ContentType
                            with get () = "json"
        
                        member this.Serialise (serialiser:ISerde) (stream:ISerdeStream) (v:Person) =
        
                            use js =
                                JsonSerialiser.Make( serialiser, stream, this.ContentType )
        
                            js.WriteStartObject()
                            js.WriteProperty "@type"
                            js.WriteValue this.TypeName
        
                            js.WriteProperty "Name"
                            js.Serialise v.Name
    
                            js.WriteEndObject()
        
                        member this.Deserialise (serialiser:ISerde) (stream:ISerdeStream) =
        
                            use jds =
                                JsonDeserialiser.Make( serialiser, stream, this.ContentType, this.TypeName )
        
                            jds.Handlers.On "Name" ( jds.ReadString )
                            
                            jds.Deserialise()
        
                            let result =
                                {
                                    Name = jds.Handlers.TryItem<_>( "Name" ).Value
                                }
        
                            result }
                        
        
