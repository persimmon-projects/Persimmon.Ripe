namespace Persimmon.Ripe

open System
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
  key: Guid,
  testCount: int) =

  let keyString = key.ToString()

  let connection = Connection.create config
  let channel = Connection.createChannel connection

  let results = ConcurrentBag<ITestResult>()

  do
    vmanager.ComputeObjectDependencies(typeof<Result>, permitCompilation = true)
    |> vmanager.LoadVagabondAssemblies
    |> ignore

  let add = function
  | Success result ->
    let result = result :?> ITestResult
    report result
    results.Add(result)
  | Failure(v, e) ->
    let metadata =
      match vmanager.Serializer.UnPickle<TestObject>(v) with
      | Context ctx -> { Name = Some ctx.Name; Parameters = []}
      | TestCase c -> { Name = c.Name; Parameters = c.Parameters}
    let result = Error(metadata, [e], [], TimeSpan.Zero) :> ITestResult
    report result
    results.Add(result)

  let receive (args: BasicDeliverEventArgs) =
    try
      vmanager.Serializer.UnPickle<Result>(args.Body)
      |> add
      |> ignore
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

  interface IDisposable with
    member this.Dispose() = this.Dispose()
