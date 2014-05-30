﻿namespace Nessos.FsPickler

    open System
    open System.Collections.Generic
    open System.IO
    open System.Numerics
    open System.Text

    open Newtonsoft.Json

    module private JsonUtils =

        let inline invalidFormat () = raise <| new InvalidDataException("invalid json format.")

        let inline writePrimitive (jsonWriter : ^JsonWriter) ignoreName (name : string) (value : ^T) =
            if not ignoreName then
                ( ^JsonWriter : (member WritePropertyName : string -> unit) (jsonWriter, name))
            ( ^JsonWriter : (member WriteValue : ^T -> unit) (jsonWriter, value))

        type JsonReader with
            member inline jsonReader.ReadProperty (name : string) =
                if jsonReader.TokenType = JsonToken.PropertyName then
                    let jsonName = jsonReader.Value |> fastUnbox<string>
                    if name <> jsonName then
                        let msg = sprintf "expected '%s' but was '%s'." name jsonName
                        raise <| new InvalidDataException(msg)
                else
                    let msg = sprintf "expected '%O' but was '%O'." JsonToken.PropertyName jsonReader.TokenType
                    raise <| new InvalidDataException(msg)

            member inline jsonReader.ValueAs<'T> () = jsonReader.Value |> fastUnbox<'T>

            member inline jsonReader.ReadPrimitiveAs<'T> ignoreName (name : string) =
                if not ignoreName then
                    jsonReader.ReadProperty name
                    jsonReader.Read() |> ignore
                
                let v = jsonReader.ValueAs<'T> ()
                jsonReader.Read() |> ignore
                v
            
            member inline jsonReader.MoveNext () = 
                if jsonReader.Read() then ()
                else
                    raise <| new EndOfStreamException()

            /// returns true iff null token
            member inline jsonReader.ReadStartObject () =
                match jsonReader.TokenType with
                | JsonToken.Null ->
                    jsonReader.Read() |> ignore
                    true
                | JsonToken.StartObject ->
                    jsonReader.Read() |> ignore
                    false
                | _ ->
                    invalidFormat ()

            member inline jsonReader.ReadEndObject () =
                if jsonReader.Read() && jsonReader.TokenType = JsonToken.EndObject then ()
                else
                    invalidFormat ()

    open JsonUtils

    type JsonPickleWriter internal (stream : Stream, encoding : Encoding, indented, leaveOpen) =
        
        let sw = new StreamWriter(stream, encoding, 1024, leaveOpen)
        let jsonWriter = new JsonTextWriter(sw) :> JsonWriter
        do jsonWriter.Formatting <- if indented then Formatting.Indented else Formatting.None

        let mutable currentValueIsNull = false

        let mutable depth = 0
        let arrayStack = new Stack<int> ()
        do arrayStack.Push Int32.MinValue
        let isArrayElement () =
            if arrayStack.Peek() = depth - 1 then true
            else
                false

        interface IPickleFormatWriter with
            
            member __.BeginWriteRoot (tag : string) =
                jsonWriter.WriteStartObject()
                writePrimitive jsonWriter false "FsPickler" AssemblyVersionInformation.Version
                writePrimitive jsonWriter false "type" tag

            member __.EndWriteRoot () = jsonWriter.WriteEnd()

            member __.BeginWriteObject (_ : TypeInfo) (_ : PicklerInfo) (tag : string) (flags : ObjectFlags) =

                if not <| isArrayElement () then
                    jsonWriter.WritePropertyName tag

                if ObjectFlags.hasFlag flags ObjectFlags.IsNull then
                    currentValueIsNull <- true
                    jsonWriter.WriteNull()
                else
                    jsonWriter.WriteStartObject()
                    depth <- depth + 1

                    if ObjectFlags.hasFlag flags ObjectFlags.IsCachedInstance then
                        writePrimitive jsonWriter false "cached" true
                    elif ObjectFlags.hasFlag flags ObjectFlags.IsCyclicInstance then
                        writePrimitive jsonWriter false "cyclic" true
                    elif ObjectFlags.hasFlag flags ObjectFlags.IsSequenceHeader then
                        writePrimitive jsonWriter false "sequence" true

                    if ObjectFlags.hasFlag  flags ObjectFlags.IsProperSubtype then
                        writePrimitive jsonWriter false "isSubtype" true

            member __.EndWriteObject () = 
                if currentValueIsNull then 
                    currentValueIsNull <- false
                else
                    depth <- depth - 1
                    jsonWriter.WriteEndObject()

            member __.BeginWriteBoundedSequence (tag : string) (length : int) =
                arrayStack.Push depth
                depth <- depth + 1

                writePrimitive jsonWriter false "length" length
                jsonWriter.WritePropertyName tag
                
                jsonWriter.WriteStartArray()

            member __.EndWriteBoundedSequence () =
                depth <- depth - 1
                arrayStack.Pop () |> ignore
                jsonWriter.WriteEndArray ()

            member __.BeginWriteUnBoundedSequence (tag : string) =
                if not <| isArrayElement () then
                    jsonWriter.WritePropertyName tag

                arrayStack.Push depth
                depth <- depth + 1

                jsonWriter.WriteStartArray()

            member __.WriteHasNextElement hasNext =
                if not hasNext then 
                    arrayStack.Pop () |> ignore
                    depth <- depth - 1
                    jsonWriter.WriteEndArray ()

            member __.WriteBoolean (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteByte (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteSByte (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value

            member __.WriteInt16 (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteInt32 (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteInt64 (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value

            member __.WriteUInt16 (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteUInt32 (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteUInt64 (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value

            member __.WriteSingle (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteDouble (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteDecimal (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value

            member __.WriteChar (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteString (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteBigInteger (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value

            member __.WriteGuid (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteDate (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag value
            member __.WriteTimeSpan (tag : string) value = writePrimitive jsonWriter (isArrayElement ()) tag <| value.ToString()

            member __.WriteBytes (tag : string) (value : byte []) = writePrimitive jsonWriter (isArrayElement ()) tag value

            member __.IsPrimitiveArraySerializationSupported = false
            member __.WritePrimitiveArray _ _ = raise <| NotSupportedException()

            member __.Dispose () = 
                jsonWriter.Flush () ; (jsonWriter :> IDisposable).Dispose()


    type JsonPickleReader internal (stream : Stream, encoding : Encoding, leaveOpen) =
        
        let sr = new StreamReader(stream, encoding, true, 1024, leaveOpen)
        let jsonReader = new JsonTextReader(sr) :> JsonReader

        let mutable currentValueIsNull = false

        let mutable depth = 0
        let arrayStack = new Stack<int> ()
        do arrayStack.Push Int32.MinValue
        let isArrayElement () =
            if arrayStack.Peek() = depth - 1 then true
            else
                false

        interface IPickleFormatReader with
            
            member __.BeginReadRoot (tag : string) =
                do jsonReader.MoveNext()

                if jsonReader.ReadStartObject () then raise <| new InvalidDataException("root json element was null.")
                else
                    let version = jsonReader.ReadPrimitiveAs<string> false "FsPickler"
                    if version <> AssemblyVersionInformation.Version then
                        raise <| new InvalidDataException(sprintf "Invalid FsPickler version %s." version)

                    let id = jsonReader.ReadPrimitiveAs<string> false "type"
                    if id <> tag then
                        let msg = sprintf "expected '%s' but was '%s'." tag id
                        raise <| new InvalidDataException()

            member __.EndReadRoot () = jsonReader.Read() |> ignore

            member __.BeginReadObject (_ : TypeInfo) (_ : PicklerInfo) (tag : string) =
                
                if not <| isArrayElement () then
                    jsonReader.ReadProperty tag
                    jsonReader.MoveNext ()

                depth <- depth + 1

                if jsonReader.ReadStartObject () then 
                    currentValueIsNull <- true
                    ObjectFlags.IsNull
                else
                    let mutable objectFlags = ObjectFlags.None

                    // peek next properties for object flags

                    match jsonReader.ValueAs<string> () with
                    | "cached" ->
                        jsonReader.MoveNext()
                        if jsonReader.ValueAs<bool> () then
                            objectFlags <- ObjectFlags.IsCachedInstance
                        jsonReader.MoveNext()

                    | "cyclic" ->
                        jsonReader.MoveNext()
                        if jsonReader.ValueAs<bool> () then
                            objectFlags <- ObjectFlags.IsCyclicInstance
                        jsonReader.MoveNext()

                    | "sequence" ->
                        jsonReader.MoveNext()
                        if jsonReader.ValueAs<bool> () then
                            objectFlags <- ObjectFlags.IsSequenceHeader
                        jsonReader.MoveNext()
                    | _ -> ()

                    match jsonReader.ValueAs<string> () with
                    | "isSubtype" ->
                        jsonReader.MoveNext()
                        if jsonReader.ValueAs<bool> () then
                            objectFlags <- objectFlags ||| ObjectFlags.IsProperSubtype
                        jsonReader.MoveNext()

                    | _ -> ()

                    objectFlags

            member __.EndReadObject () =
                depth <- depth - 1

                if currentValueIsNull then 
                    currentValueIsNull <- false
                else 
                    jsonReader.MoveNext()

            member __.BeginReadBoundedSequence tag =
                arrayStack.Push depth
                depth <- depth + 1

                let length = jsonReader.ReadPrimitiveAs<int64> false "length"
                jsonReader.ReadProperty tag
                jsonReader.MoveNext()

                if jsonReader.TokenType = JsonToken.StartArray then
                    jsonReader.MoveNext()
                    int length
                else
                    raise <| new InvalidDataException("expected array.")

            member __.EndReadBoundedSequence () =
                if jsonReader.TokenType = JsonToken.EndArray && jsonReader.Read () then
                    arrayStack.Pop () |> ignore
                    depth <- depth - 1
                else
                    raise <| InvalidDataException("expected end of array.")

            member __.BeginReadUnBoundedSequence tag =
                arrayStack.Push depth
                depth <- depth + 1

                jsonReader.ReadProperty tag

                if jsonReader.Read () && jsonReader.TokenType = JsonToken.StartArray then
                    jsonReader.MoveNext()
                else
                    raise <| new InvalidDataException("expected array.")

            member __.ReadHasNextElement () =
                if jsonReader.TokenType = JsonToken.EndArray && jsonReader.Read () then
                    arrayStack.Pop () |> ignore
                    depth <- depth - 1
                    false
                else
                    true

            member __.ReadBoolean tag = jsonReader.ReadPrimitiveAs<bool> (isArrayElement ()) tag

            member __.ReadByte tag = jsonReader.ReadPrimitiveAs<int64> (isArrayElement ()) tag |> byte
            member __.ReadSByte tag = jsonReader.ReadPrimitiveAs<int64> (isArrayElement ()) tag |> sbyte

            member __.ReadInt16 tag = jsonReader.ReadPrimitiveAs<int64> (isArrayElement ()) tag |> int16
            member __.ReadInt32 tag = jsonReader.ReadPrimitiveAs<int64> (isArrayElement ()) tag |> int
            member __.ReadInt64 tag = jsonReader.ReadPrimitiveAs<int64> (isArrayElement ()) tag

            member __.ReadUInt16 tag = jsonReader.ReadPrimitiveAs<int64> (isArrayElement ()) tag |> uint16
            member __.ReadUInt32 tag = jsonReader.ReadPrimitiveAs<int64> (isArrayElement ()) tag |> uint32
            member __.ReadUInt64 tag = jsonReader.ReadPrimitiveAs<int64> (isArrayElement ()) tag |> uint64

            member __.ReadSingle tag = jsonReader.ReadPrimitiveAs<double> (isArrayElement ()) tag |> single
            member __.ReadDouble tag = jsonReader.ReadPrimitiveAs<double> (isArrayElement ()) tag

            member __.ReadChar tag = let value = jsonReader.ReadPrimitiveAs<string> (isArrayElement ()) tag in value.[0]
            member __.ReadString tag = jsonReader.ReadPrimitiveAs<string> (isArrayElement ()) tag
            member __.ReadBigInteger tag = jsonReader.ReadPrimitiveAs<string> (isArrayElement ()) tag |> BigInteger.Parse

            member __.ReadGuid tag = jsonReader.ReadPrimitiveAs<string> (isArrayElement ()) tag |> Guid.Parse
            member __.ReadTimeSpan tag = jsonReader.ReadPrimitiveAs<string> (isArrayElement ()) tag |> TimeSpan.Parse
            
            member __.ReadDate tag = 
                if not <| isArrayElement () then
                    jsonReader.ReadProperty tag

                let d = jsonReader.ReadAsDateTime().Value
                jsonReader.MoveNext()
                d

            member __.ReadDecimal tag =
                if not <| isArrayElement () then
                    jsonReader.ReadProperty tag
                    
                let d = jsonReader.ReadAsDecimal().Value
                jsonReader.MoveNext()
                d

            member __.ReadBytes tag = 
                if not <| isArrayElement () then
                    jsonReader.ReadProperty tag
                   
                let bytes = jsonReader.ReadAsBytes() 
                jsonReader.MoveNext()
                bytes

            member __.IsPrimitiveArraySerializationSupported = false
            member __.ReadPrimitiveArray _ _ = raise <| new NotImplementedException()

            member __.Dispose () = (jsonReader :> IDisposable).Dispose() ; sr.Dispose()

    type JsonPickleFormatProvider (?encoding : Encoding, ?indented, ?leaveOpen) =
        let encoding = defaultArg encoding Encoding.UTF8
        let leaveOpen = defaultArg leaveOpen true
        let indented = defaultArg indented false

        interface IPickleFormatProvider with
            member __.CreateWriter(stream) = new JsonPickleWriter(stream, encoding, indented, leaveOpen) :> _
            member __.CreateReader(stream) = new JsonPickleReader(stream, encoding, leaveOpen) :> _