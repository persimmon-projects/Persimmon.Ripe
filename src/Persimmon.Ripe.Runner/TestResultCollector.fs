namespace Persimmon.Ripe.Runner

open System
open System.IO
open System.Collections.Concurrent
open Nessos.Vagabond
open Persimmon
open Persimmon.Ripe

[<AbstractClass>]
type TestResultCollector<'Config, 'Key>
  (
  config: 'Config,
  vmanager: VagabondManager,
  report: ITestResult -> unit,
  keyString: string,
  testCount: int) =

  let fakeReporter = TextWriter.Null

  let results = ConcurrentBag<ITestResult>()

  do
    vmanager.ComputeObjectDependencies(typeof<Result>, permitCompilation = true)
    |> vmanager.LoadVagabondAssemblies
    |> ignore

  member __.AddOrRetryF(getBody: unit -> byte [], retry: Test -> unit) =
    match vmanager.Serializer.UnPickle<Result>(getBody ()) with
    | Success result ->
      let result = result :?> ITestResult
      report result
      results.Add(result)
    | Failure(v, e) ->
      match vmanager.Serializer.UnPickle<Test>(v) with
      | { Retry = 0; Run = f } ->
        let result = f fakeReporter :?> ITestResult
        report result
        results.Add(result)
      | t -> { t with Retry = t.Retry - 1 } |> retry

  // TODO: rename
  member __.Results =
    if results.Count = testCount then
      Complete(results)
    else Incomplete

  abstract member ReceiveResult: unit -> unit

  member this.StartConsume() =
    this.ReceiveResult()

  abstract member Dispose: unit -> unit
  default __.Dispose() = fakeReporter.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
