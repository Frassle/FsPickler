(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../src/FsPickler.Json/bin/Release/netstandard2.0/"

(**

# FsPickler : A fast, multi-format messaging serializer for .NET

FsPickler is a serialization library that facilitates the distribution of objects across .NET processes.
The implementation focuses on performance and supporting as many types as possible, where possible.
It supports multiple, pluggable serialization formats such as XML, JSON and BSON;
also included is a fast binary format of its own.
The library is based on the functional programming concept of 
[pickler combinators](http://lambda-the-ultimate.org/node/2243)
which has been adapted to accommodate the .NET type system.

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The FsPickler library can be <a href="https://nuget.org/packages/FsPickler">installed from NuGet</a>:
      <pre>PM> Install-Package FsPickler
PM> Install-Package FsPickler.Json</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

## Example

This example demonstrates a basic serialization roundtrip using the library

*)
#r "FsPickler.dll"
open MBrace.FsPickler

let binarySerializer = FsPickler.CreateBinarySerializer()

let pickle = binarySerializer.Pickle [Some 1; None ; Some -1]
binarySerializer.UnPickle<int option list> pickle

(**

## Why FsPickler?

The principal motivation behind creating FsPickler is the need for a
library that provides efficient, correct and complete serialization of
objects in the CLR and mono runtime. It is aimed at providing a foundation for
efficient communication across homogeneous .NET clusters.

FsPickler is ideally suited for serializing:

 * Large and complex object graphs, such as dictionaries, trees and cyclic graphs.

 * Abstract classes, subtypes, delegates and closures.

 * ISerializable, DataContract or attribute-based serialization.

 * F# unions, records and quotations.

 * Inaccessible types or types unknown at compile time.

FsPickler is NOT:

 * a general-purpose XML/JSON/BSON framework.

 * a library designed for cross-platform communication.

 * a library designed with version tolerance in mind. Avoid using for long-term persistence.

## Documentation & Technical Overview

A collection of tutorials, technical overviews and API references of the library.

 * [Tutorial](tutorial.html) A short introduction to FsPickler.

 * [Technical Overview](overview.html) A walkthrough of the library's implementation details.

 * OUTDATED [Performance](benchmarks.html) Benchmarks comparing FsPickler
   to other established serialization libraries.

 * [.NET Core Benchmarks](https://github.com/mbraceproject/FsPickler/wiki/.NET-Core-Benchmarks)

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library.

## Who uses FsPickler?

* [MBrace framework](http://mbrace.io/) - MBrace is a framework for distributed
  computation in the cloud. Its programming model uses continuations, so a library
  that supports serialization of closures was essential. FsPickler was designed out
  of this requirement.

* [Akka.NET](http://akkadotnet.github.io/) - Used in the Akka.FSharp library for its
  quotation serialization capabilities.

* [Suave.IO](http://suave.io/) - "we needed a simple way of serialising CLR types to
  put in cookies. After fighting .Net's JSONDataContractSerializer for a good while
  we tried FsPickler. It was a straight success; it just worked".

* [Tachyus](http://www.tachyus.com/) - "FsPickler serializes and deserializes
  objects of virtually any type quickly and reliably in a single line of
  F# code. For us this makes communications between applications across
  computing environments effortless. There are no implementation details
  to obsess about".
 
## Contributing and copyright

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests.

The library is available under the MIT License. 
For more information see the [License file][license] in the GitHub repository. 

  [gh]: https://github.com/mbraceproject/FsPickler
  [issues]: https://github.com/mbraceproject/FsPickler/issues
  [license]: https://github.com/mbraceproject/FsPickler/blob/master/License.md
*)
