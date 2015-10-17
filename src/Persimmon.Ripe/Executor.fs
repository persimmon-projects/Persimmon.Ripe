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

  member __.Connect() =
    channel.ExchangeDeclare(RabbitMQ.Exchange, RabbitMQ.Topic)
    let queueName = channel.QueueDeclare(RabbitMQ.Queue.TestCase, false, false, false, null).QueueName
    channel.QueueBind(queueName, RabbitMQ.Exchange, "#")
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(fun args ->
      let result =
        try
          let o = serializer.UnPickle<TestObject>(args.Body)
          Success(run o)
        with e -> Failure(args.Body, e)
      result
      |> serializer.Pickle
      |> Publisher.publish publisher RabbitMQ.Queue.Result args.RoutingKey
    )
    channel.BasicConsume(queueName, true, consumer) |> ignore

  member __.Dispose() =
    publisher.Dispose()
    channel.Dispose()
    connection.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
