namespace Persimmon.Ripe

open System
open System.Collections.Concurrent
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.FsPickler
open Config
open Persimmon
open Persimmon.ActivePatterns

type ResultCollector(config: RabbitMQ, report: ITestResult -> unit, key: Guid, testCount: int) =

  let keyString = key.ToString()

  let connection = Connection.create config
  let channel = Connection.createChannel connection
  let serializer = FsPickler.CreateBinarySerializer()

  let results = ConcurrentBag<ITestResult>()

  let add = function
  | Success result ->
    report result
    results.Add(result)
  | Failure(v, e) ->
    let metadata =
      match serializer.UnPickle<TestObject>(v) with
      | Context ctx -> { Name = ctx.Name; Parameters = []}
      | TestCase c -> { Name = c.Name; Parameters = c.Parameters}
    let result = Error(metadata, [e], [], TimeSpan.Zero) :> ITestResult
    report result
    results.Add(result)

  // rename
  member __.Results =
    if results.Count = testCount then
      Complete(results)
    else Incomplete

  member __.Connect() =
    channel.BasicQos(0u, 1us, false)
    channel.ExchangeDeclare(RabbitMQ.Exchange, RabbitMQ.Topic)
    let queueName = channel.QueueDeclare(RabbitMQ.Queue.Result, false, false, false, null).QueueName
    channel.QueueBind(queueName, RabbitMQ.Exchange, sprintf "%s.%s" RabbitMQ.Queue.Result keyString)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(fun args ->
      serializer.UnPickle<Result>(args.Body)
      |> add
      |> ignore
      channel.BasicAck(args.DeliveryTag, false)
    )
    channel.BasicConsume(queueName, false, consumer) |> ignore

  member __.Dispose() =
    channel.Dispose()
    connection.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
