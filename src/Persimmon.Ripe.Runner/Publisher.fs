namespace Persimmon.Ripe.Runner

open System
open Nessos.Vagabond

[<AbstractClass>]
type TestPublisher<'Config, 'Key>(config: 'Config, vmanager: VagabondManager) =

  abstract member Publish: 'Key * byte [] -> unit
  abstract member Dispose: unit -> unit

  member this.PublishTest(routingKey, value) =
    this.Publish(routingKey, vmanager.Serializer.Pickle(value))

  interface IDisposable with
    member this.Dispose() = this.Dispose()
