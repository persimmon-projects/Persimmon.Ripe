namespace Persimmon.Ripe

open System
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.Vagabond
open Config
open Persimmon
open Persimmon.ActivePatterns

type Executor(config: RabbitMQ, vmanager: VagabondManager, writer: IO.TextWriter) =

  let connection = Connection.create config
  let channel = Connection.createChannel connection
  let publisher = new Publisher(config, vmanager)

  do
    fprintfn writer "connected rabbitmq: %s" config.Uri

    vmanager.ComputeObjectDependencies(typeof<Result>, permitCompilation = true)
    |> vmanager.LoadVagabondAssemblies
    |> Seq.iter (fprintfn writer "%A")

  let resultKey (key: string) =
    match key.Split([|'.'|]) with
    | [|_; key|] -> sprintf "%s.%s" RabbitMQ.Queue.Result key
    | _ -> RabbitMQ.Queue.Result

  let receiveAssemblies (args: BasicDeliverEventArgs) =
    try
      vmanager.Serializer.UnPickle<VagabondAssembly []>(args.Body)
      |> vmanager.LoadVagabondAssemblies
      |> Seq.iter (fprintfn writer "%A")
    with e -> fprintfn writer "%A" e

  let consumeAssemblies () =
    channel.ExchangeDeclare(RabbitMQ.Exchange, RabbitMQ.Topic)
    let queueName = channel.QueueDeclare(RabbitMQ.Queue.Assemblies, false, false, false, null).QueueName
    channel.QueueBind(queueName, RabbitMQ.Exchange, sprintf "%s.*" RabbitMQ.Queue.Assemblies)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(receiveAssemblies)
    channel.BasicConsume(queueName, true, consumer) |> ignore

  let receiveTest (args: BasicDeliverEventArgs) =
    let result =
      try
        let f = vmanager.Serializer.UnPickle<unit -> obj>(args.Body)
        Success(f ())
      with e -> Failure(args.Body, e)
    result
    |> Publisher.publish publisher RabbitMQ.Queue.Result (resultKey args.RoutingKey)
    channel.BasicAck(args.DeliveryTag, false)

  let consumeTest () =
    channel.BasicQos(0u, 1us, false)
    channel.ExchangeDeclare(RabbitMQ.Exchange, RabbitMQ.Topic)
    let queueName = channel.QueueDeclare(RabbitMQ.Queue.TestCase, false, false, false, null).QueueName
    channel.QueueBind(queueName, RabbitMQ.Exchange, sprintf "%s.*" RabbitMQ.Queue.TestCase)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(receiveTest)
    channel.BasicConsume(queueName, false, consumer) |> ignore

  member __.StartConsume() =
    consumeAssemblies ()
    consumeTest ()

  member __.Dispose() =
    publisher.Dispose()
    channel.Dispose()
    connection.Dispose()
    writer.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()
