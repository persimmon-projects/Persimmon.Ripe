namespace Persimmon.Ripe

open System
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.FsPickler
open Config
open Persimmon
open Persimmon.ActivePatterns

type Executor(config: RabbitMQ, writer: IO.TextWriter) =

  let connection = Connection.create config
  let channel = Connection.createChannel connection
  let serializer = FsPickler.CreateBinarySerializer()
  let publisher = new Publisher(config)

  let rec writeResult prefix = function
  | ContextResult ctx ->
    ctx.Children
    |> Seq.iter (writeResult (sprintf "%s.%s." prefix ctx.Name))
  | EndMarker -> writer.WriteLine()
  | TestResult tr -> fprintfn writer "end test: %s%s" prefix tr.FullName

  let run = function
  | Context ctx -> ctx.Run(writeResult (sprintf "%s." ctx.Name)) :> ITestResult
  | TestCase tc ->
    let result = tc.Run() :> ITestResult
    writeResult "" result
    result

  let resultKey (key: string) =
    match key.Split([|'.'|]) with
    | [|_; key|] -> sprintf "%s.%s" RabbitMQ.Queue.Result key
    | _ -> RabbitMQ.Queue.Result

  let receive (args: BasicDeliverEventArgs) =
    let result =
      try
        let o = serializer.UnPickle<TestObject>(args.Body)
        Success(run o)
      with e -> Failure(args.Body, e)
    result
    |> Publisher.publish publisher RabbitMQ.Queue.Result (resultKey args.RoutingKey)
    channel.BasicAck(args.DeliveryTag, false)

  member __.Connect() =
    channel.BasicQos(0u, 1us, false)
    channel.ExchangeDeclare(RabbitMQ.Exchange, RabbitMQ.Topic)
    let queueName = channel.QueueDeclare(RabbitMQ.Queue.TestCase, false, false, false, null).QueueName
    channel.QueueBind(queueName, RabbitMQ.Exchange, sprintf "%s.*" RabbitMQ.Queue.TestCase)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(receive)
    channel.BasicConsume(queueName, false, consumer) |> ignore

  member __.Dispose() =
    publisher.Dispose()
    channel.Dispose()
    connection.Dispose()
    writer.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
