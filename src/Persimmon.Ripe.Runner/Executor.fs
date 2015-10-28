namespace Persimmon.Ripe.Runner

open System
open System.IO
open Nessos.Vagabond
open Persimmon.Ripe

[<AbstractClass>]
type TestExecutor<'Config, 'Key>(config: 'Config, vmanager: VagabondManager, writer: TextWriter) as this =

  do
    vmanager.ComputeObjectDependencies(this.GetType(), permitCompilation = true)
    |> vmanager.LoadVagabondAssemblies
    |> Seq.iter (fprintfn writer "%A")

  member __.LoadAssemblies(getBody: unit -> byte []) =
    try
      vmanager.Serializer.UnPickle<VagabondAssembly []>(getBody ())
      |> vmanager.LoadVagabondAssemblies
      |> Seq.iter (fprintfn writer "%A")
    with e -> fprintfn writer "%A" e

  member __.RunTest(getBody: unit -> byte []) =
    let result =
      let body = getBody ()
      try
        let t = vmanager.Serializer.UnPickle<Test>(body)
        Success(t.Run writer)
      with e -> Failure(body, e)
    result

  abstract member ConsumeAssemblies: unit -> unit
  abstract member ConsumeTest: unit -> unit

  member __.StartConsume() =
    this.ConsumeAssemblies()
    this.ConsumeTest()

  abstract member Dispose: unit -> unit
  default __.Dispose() = writer.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
