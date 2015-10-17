namespace Persimmon.Ripe

open System
open System.Collections.Concurrent
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.FsPickler
open Config
open Persimmon
open Persimmon.ActivePatterns

type ResultCollector(config: RabbitMQ, report: ITestResult -> unit, tests: Map<Guid, TestObject>) =

  let connection = Connection.create config
  let channel = Connection.createChannel connection
  let serializer = FsPickler.CreateBinarySerializer()
  let publisher = new Publisher(config)

  let results = ConcurrentDictionary<Guid, ITestResult>()

  let add = function
  | Success(guid, result) ->
    report result
    results.TryAdd(guid, result)
  | Failure(v, e) ->
    let guid, t = serializer.UnPickle<Guid * TestObject>(v)
    let metadata =
      match t with
      | Context ctx -> { Name = ctx.Name; Parameters = []}
      | TestCase c -> { Name = c.Name; Parameters = c.Parameters}
    let result = Error(metadata, [e], [], TimeSpan.Zero) :> ITestResult
    report result
    results.TryAdd(guid, result)

  // rename
  member __.Results =
    if tests |> Map.forall (fun g1 _ -> results |> Seq.exists (fun (KeyValue(g2, _)) -> g1 = g2)) then
      Complete(results.Values)
    else Incomplete

  member __.Connect() =
    channel.ExchangeDeclare(RabbitMQ.Exchange, "topic")
    let queueName = channel.QueueDeclare().QueueName
    channel.QueueBind(queueName, RabbitMQ.Exchange, "result")
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(fun args ->
      serializer.UnPickle<Result>(args.Body)
      |> add
      |> ignore
    )
    channel.BasicConsume(queueName, false, consumer) |> ignore

  member __.Dispose() =
    publisher.Dispose()
    channel.Dispose()
    connection.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
