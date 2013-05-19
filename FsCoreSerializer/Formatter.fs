﻿namespace FsCoreSerializer

    open System
    open System.IO
    open System.Text
    open System.Runtime.CompilerServices
    open System.Collections.Generic
    open System.Runtime.Serialization


    // each reference type is serialized with a 32 bit header
    //   1. the first byte is a fixed identifier
    //   2. the next two bytes are a truncated hash which identifies the type being serialized
    //   3. the third byte conveys serialization specific switches
    //
    module internal ObjHeader =

        // literals are ridiculously faster than traditional enums

        [<Literal>]
        let initByte = 130uy

        // control bits
        [<Literal>]
        let empty               = 0uy
        [<Literal>]
        let isNull              = 0b00000001uy
        [<Literal>]
        let isProperSubtype     = 0b00000010uy
        [<Literal>]
        let isNewInstance       = 0b00000100uy
        [<Literal>]
        let isCachedInstance    = 0b00001000uy

        let getTruncatedHash (t : Type) =
            let n = (TypeSerializer.Default.Write t).GetHashCode()
            uint16 n ^^^ uint16 (n >>> 16)

        let inline hasFlag (h : byte) (flag : byte) = h &&& flag = flag
        
        let inline create (hash : uint16) (flags : byte) =
            uint32 initByte ||| (uint32 hash <<< 8) ||| (uint32 flags <<< 24)

        let inline read (hash : uint16) (header : uint32) =
            if byte header <> initByte || uint16 (header >>> 8) <> hash then 
                raise <| new SerializationException("stream error")
            else 
                byte (header >>> 24)


    type TypeInfo =
        | Primitive = 0
        | Enum = 1
        | Value = 2
        | Array = 3
        | Sealed = 4
        | NonSealed = 5
        | Abstract = 6

    type FormatterInfo =
        | Atomic = 0
        | ReflectionType = 1
        | ReflectionDerived = 2
        | ISerializable = 3
        | FSharpValue = 4 // tuples, records and DUs
        | IFsCoreSerializable = 5
        | Custom = 6

    type Formatter =
        internal {
            Type : Type

            Write : Writer -> obj -> unit
            Read : Reader -> obj

            TypeInfo : TypeInfo
            TypeHash : uint16
            FormatterInfo : FormatterInfo

            CacheObj : bool
            UseWithSubtypes : bool
        }   

    and [<Sealed>] Writer internal (bw : BinaryWriter, typeFormatter : Formatter,
                                        resolver : Type -> Formatter, sc : StreamingContext) =
        
        do assert(typeFormatter.Type = typeof<Type>)

        let idGen = new ObjectIDGenerator()
        let objStack = new Stack<int64> ()

        member w.BW = bw
        member w.StreamingContext = sc
        member w.WriteObj (f : Formatter, o : obj) =
            do RuntimeHelpers.EnsureSufficientExecutionStack()

            let inline writeHeader (flags : byte) =
                bw.Write(ObjHeader.create f.TypeHash flags)

            if obj.ReferenceEquals(o, null) then writeHeader ObjHeader.isNull
            elif f.TypeInfo <= TypeInfo.Value then 
                writeHeader ObjHeader.empty
                f.Write w o
            elif f.CacheObj then
                let mutable firstOccurence = false
                let id = idGen.GetId(o, &firstOccurence)
                if firstOccurence then
                    let inline writeObj (f : Formatter) =
                        if f.FormatterInfo = FormatterInfo.ReflectionDerived then
                            f.Write w o
                        else
                            objStack.Push id
                            f.Write w o
                            objStack.Pop () |> ignore

                    if f.TypeInfo <= TypeInfo.Sealed || f.UseWithSubtypes then
                        writeHeader ObjHeader.isNewInstance
                        writeObj f
                    else
                        // type is not sealed, do subtype resolution
                        let t0 = o.GetType()
                        if t0 <> f.Type then
                            let f0 = resolver t0
                            writeHeader (ObjHeader.isNewInstance ||| ObjHeader.isProperSubtype)
                            w.WriteObj(typeFormatter, t0)
                            writeObj f0
                        else
                            writeHeader ObjHeader.isNewInstance
                            writeObj f

                elif objStack.Contains id then
                    raise <| new SerializationException(sprintf "Unexpected cyclic object graph [%s]." f.Type.FullName)
                else
                    writeHeader ObjHeader.isCachedInstance
                    bw.Write id
            else
                if f.TypeInfo <= TypeInfo.Sealed || f.UseWithSubtypes then
                    writeHeader ObjHeader.empty
                    f.Write w o
                else
                    // type is not sealed, do subtype resolution
                    let t0 = o.GetType ()
                    if t0 <> f.Type then
                        let f0 = resolver t0
                        writeHeader ObjHeader.isProperSubtype
                        f0.Write w o
                    else
                        writeHeader ObjHeader.empty

        member w.Write<'T>(t : 'T) = let f = resolver typeof<'T> in w.WriteObj(f, t)
        member w.WriteObj(t : Type, o : obj) = let f = resolver t in w.WriteObj(f, o)
        member w.WriteObj(o : obj) =
            if obj.ReferenceEquals(o, null) then bw.Write (ObjHeader.create 0us ObjHeader.isNull)
            else
                let t = o.GetType()
                let f = resolver t
                bw.Write (ObjHeader.create 0us ObjHeader.empty)
                w.WriteObj(typeFormatter, t)
                w.WriteObj(f, o)

    and [<Sealed>] Reader internal (br : BinaryReader, typeFormatter : Formatter, 
                                        resolver : Type -> Formatter, sc : StreamingContext) =

        do assert(typeFormatter.Type = typeof<Type>)

        let objCache = new Dictionary<int64, obj> ()
        let mutable counter = 1L
        let mutable currentReflectedObjId = 0L

        member internal r.EarlyRegisterObject (o : obj) =
            if currentReflectedObjId = 0L then
                raise <| new SerializationException("Unexpected reflected object binding.")
            else
                objCache.Add(currentReflectedObjId, o)
                currentReflectedObjId <- 0L

        member r.BR = br
        member r.StreamingContext = sc

        member r.ReadObj(f : Formatter) =
            let flags = ObjHeader.read f.TypeHash (br.ReadUInt32())

            if ObjHeader.hasFlag flags ObjHeader.isNull then null
            elif f.TypeInfo <= TypeInfo.Value then f.Read r
            elif ObjHeader.hasFlag flags ObjHeader.isNewInstance then
                let id = counter
                counter <- counter + 1L

                let inline readObj (f : Formatter) =
                    let inline checkState () =
                        if currentReflectedObjId <> 0L then
                            raise <| new SerializationException("Internal error: reader state is corrupt.")
                        
                    if f.FormatterInfo = FormatterInfo.ReflectionDerived then
                        do checkState ()

                        currentReflectedObjId <- id
                        let o = f.Read r

                        do checkState ()
                        o
                    else
                        let o = f.Read r
                        objCache.Add(id, o) ; o
                
                if ObjHeader.hasFlag flags ObjHeader.isProperSubtype then
                    let t0 = r.ReadObj typeFormatter :?> Type
                    let f0 = resolver t0
                    readObj f0
                else
                    readObj f

            elif ObjHeader.hasFlag flags ObjHeader.isCachedInstance then
                let id = br.ReadInt64() in objCache.[id]

            elif ObjHeader.hasFlag flags ObjHeader.isProperSubtype then
                let t0 = r.ReadObj typeFormatter :?> Type
                let f0 = resolver t0
                f0.Read r
            else
                f.Read r
        
        member r.ReadObj (t : Type) = let f = resolver t in r.ReadObj f
        member r.Read<'T> () = let f = resolver typeof<'T> in r.ReadObj f :?> 'T
        member r.ReadObj () =
            let flags = ObjHeader.read 0us (br.ReadUInt32())
            if ObjHeader.hasFlag flags ObjHeader.isNull then null
            else
                let t = r.ReadObj typeFormatter :?> Type
                let f = resolver t
                r.ReadObj f


    // interface section

    type IFsCoreSerializable =
        // requires ctor : Reader -> IFsCoreSerializable
        abstract GetObjectData : Writer -> unit

    and IFormatterFactory =
        abstract Create : (Type -> Lazy<Formatter>) -> Formatter

    and IGenericFormatterFactory = interface end

    and IGenericFormatterFactory1 =
        inherit IGenericFormatterFactory
        abstract Create<'T> : (Type -> Lazy<Formatter>) -> Formatter

    and IGenericFormatterFactory2 =
        inherit IGenericFormatterFactory
        abstract Create<'T, 'S> : (Type -> Lazy<Formatter>) -> Formatter

    and IGenericFormatterFactory3 =
        inherit IGenericFormatterFactory
        abstract Create<'T, 'S, 'U> : (Type -> Lazy<Formatter>) -> Formatter

    and IGenericFormatterFactory4 =
        inherit IGenericFormatterFactory
        abstract Create<'T, 'S, 'U, 'V> : (Type -> Lazy<Formatter>) -> Formatter