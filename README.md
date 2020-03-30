## FsPickler

[![Join the chat at https://gitter.im/mbraceproject/FsPickler](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/mbraceproject/FsPickler?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![NuGet Status](http://img.shields.io/nuget/vpre/FsPickler.svg?style=flat)](https://www.nuget.org/packages/FsPickler/)

FsPickler is a serialization library that facilitates the distribution of .NET objects.
The implementation focuses on performance and completeness in supported types, including F# types.
It supports multiple, pluggable serialization formats including Binary, Xml, JSON and BSON.
The library is based on the functional programming concept of 
[pickler combinators](https://www.microsoft.com/en-us/research/publication/functional-pearl-pickler-combinators/) 
which has been adapted to accommodate the object oriented nature of the .NET framework.

Packages of the library are available on Nuget [[1](http://www.nuget.org/packages/FsPickler),[2](http://www.nuget.org/packages/FsPickler.Json)].

### Documentation

* [Tutorial](http://mbraceproject.github.io/FsPickler/tutorial.html) A short introduction to FsPickler.
* [Technical Overview](http://mbraceproject.github.io/FsPickler/overview.html) A walkthrough of the library's implementation details.
* [New .NET Core Benchmarks](https://github.com/mbraceproject/FsPickler/wiki/.NET-Core-Benchmarks).
* [API Reference](http://mbraceproject.github.io/FsPickler/reference/index.html) Auto-generated library documentation.

All documentation and related material can be found [here](http://mbraceproject.github.io/FsPickler/).

### Build Status

Head (branch `master`), Build & Unit tests

* Windows [![Build status](https://ci.appveyor.com/api/projects/status/0wp9nbg4942q1ner?svg=true)](https://ci.appveyor.com/project/nessos/fspickler)
* Linux [![Build Status](https://travis-ci.org/mbraceproject/FsPickler.svg?branch=master)](https://travis-ci.org/mbraceproject/FsPickler/branches)
