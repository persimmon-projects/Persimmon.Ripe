namespace Persimmon.Ripe

open System
open System.IO
open System.Collections.Concurrent
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.Vagabond
open Config
open Persimmon
open Persimmon.ActivePatterns

type ResultCollector
  (
  config: RabbitMQ,
  vmanager: VagabondManager,
  report: ITestResult -> unit,
  keyString: string,
  testCount: int) =

  let testCaseKey = sprintf "%s.%s" Config.RabbitMQ.Queue.TestCase keyString

  let connection = Connection.create config
  let channel = Connection.createChannel connection
  let publisher = new Publisher(config, vmanager)

  let results = ConcurrentBag<ITestResult>()

  let fakeReporter = TextWriter.Null

  do
    vmanager.ComputeObjectDependencies(typeof<Result>, permitCompilation = true)
    |> vmanager.LoadVagabondAssemblies
    |> ignore

  let addOrRetry = function
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
    | t ->
      { t with Retry = t.Retry - 1 }
      |> Publisher.publish publisher Config.RabbitMQ.Queue.TestCase testCaseKey

  let receive (args: BasicDeliverEventArgs) =
    try
      vmanager.Serializer.UnPickle<Result>(args.Body)
      |> addOrRetry
      channel.BasicAck(args.DeliveryTag, false)
    with e -> printfn "%A" e

  // rename
  member __.Results =
    if results.Count = testCount then
      Complete(results)
    else Incomplete

  member __.StartConsume() =
    channel.BasicQos(0u, 1us, false)
    channel.ExchangeDeclare(RabbitMQ.Exchange, RabbitMQ.Topic)
    let queueName = channel.QueueDeclare(RabbitMQ.Queue.Result, false, false, false, null).QueueName
    channel.QueueBind(queueName, RabbitMQ.Exchange, sprintf "%s.%s" RabbitMQ.Queue.Result keyString)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(receive)
    channel.BasicConsume(queueName, false, consumer) |> ignore

  member __.Dispose() =
    channel.Dispose()
    connection.Dispose()
    publisher.Dispose()
    fakeReporter.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
