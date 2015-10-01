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
    channel.ExchangeDeclare(RabbitMQ.Exchange, "topic")
    channel.QueueBind("", RabbitMQ.Exchange, "testcase")
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(fun args ->
      try
        let key, o = serializer.UnPickle<Guid * TestObject>(args.Body)
        Success(key, run o)
      with e -> Failure(args.Body, e)
      |> serializer.Pickle
      |> Publisher.publish publisher "result"
    )
    channel.BasicConsume("", false, consumer) |> ignore

  member __.Dispose() =
    publisher.Dispose()
    channel.Dispose()
    connection.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
