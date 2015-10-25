namespace Persimmon.Ripe.RabbitMQ

open System
open System.IO
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.Vagabond
open Persimmon
open Persimmon.ActivePatterns
open Persimmon.Ripe

type Executor(config: Config, vmanager: VagabondManager, writer: TextWriter) =

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
    | [|_; key|] -> sprintf "%s.%s" Constant.Queue.Result key
    | _ -> Constant.Queue.Result

  let receiveAssemblies (args: BasicDeliverEventArgs) =
    try
      vmanager.Serializer.UnPickle<VagabondAssembly []>(args.Body)
      |> vmanager.LoadVagabondAssemblies
      |> Seq.iter (fprintfn writer "%A")
    with e -> fprintfn writer "%A" e

  let consumeAssemblies () =
    channel.ExchangeDeclare(Constant.Exchange, Constant.Topic)
    let queueName = channel.QueueDeclare(Constant.Queue.Assemblies, false, false, false, null).QueueName
    channel.QueueBind(queueName, Constant.Exchange, sprintf "%s.*" Constant.Queue.Assemblies)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(receiveAssemblies)
    channel.BasicConsume(queueName, true, consumer) |> ignore

  let receiveTest (args: BasicDeliverEventArgs) =
    let result =
      try
        let t = vmanager.Serializer.UnPickle<Test>(args.Body)
        Success(t.Run writer)
      with e -> Failure(args.Body, e)
    result
    |> Publisher.publish publisher Constant.Queue.Result (resultKey args.RoutingKey)
    channel.BasicAck(args.DeliveryTag, false)

  let consumeTest () =
    channel.BasicQos(0u, 1us, false)
    channel.ExchangeDeclare(Constant.Exchange, Constant.Topic)
    let queueName = channel.QueueDeclare(Constant.Queue.TestCase, false, false, false, null).QueueName
    channel.QueueBind(queueName, Constant.Exchange, sprintf "%s.*" Constant.Queue.TestCase)
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
