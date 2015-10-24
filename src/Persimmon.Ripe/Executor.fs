namespace Persimmon.Ripe

open System
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.FsPickler
open Config
open Persimmon
open Persimmon.ActivePatterns

type Executor(config: RabbitMQ) =

  let connection = Connection.create config
  let channel = Connection.createChannel connection
  let serializer = FsPickler.CreateBinarySerializer()
  let publisher = new Publisher(config)

  let run = function
  | Context ctx -> ctx.Run(ignore) :> ITestResult
  | TestCase tc -> tc.Run() :> ITestResult

  let resultKey (key: string) =
    match key.Split([|'.'|]) with
    | [|_; key|] -> sprintf "%s.%s" RabbitMQ.Queue.Result key
    | _ -> RabbitMQ.Queue.Result

  member __.Connect() =
    channel.BasicQos(0u, 1us, false)
    channel.ExchangeDeclare(RabbitMQ.Exchange, RabbitMQ.Topic)
    let queueName = channel.QueueDeclare(RabbitMQ.Queue.TestCase, false, false, false, null).QueueName
    channel.QueueBind(queueName, RabbitMQ.Exchange, sprintf "%s.*" RabbitMQ.Queue.TestCase)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(fun args ->
      try
        let o = serializer.UnPickle<TestObject>(args.Body)
        Success(run o)
      with e -> Failure(args.Body, e)
      |> Publisher.publish publisher RabbitMQ.Queue.Result (resultKey args.RoutingKey)
      channel.BasicAck(args.DeliveryTag, false)
    )
    channel.BasicConsume(queueName, false, consumer) |> ignore

  member __.Dispose() =
    publisher.Dispose()
    channel.Dispose()
    connection.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
