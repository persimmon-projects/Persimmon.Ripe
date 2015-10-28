namespace Persimmon.Ripe.RabbitMQ

open System.IO
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.Vagabond
open Persimmon.Ripe.Runner

type Executor(config: Config, vmanager: VagabondManager, writer: TextWriter) =
  inherit TestExecutor<Config, Routing>(config, vmanager, writer)

  let connection = Connection.create config
  let channel = Connection.createChannel connection
  let publisher = new Publisher(config, vmanager)

  do
    fprintfn writer "connected rabbitmq: %s" config.Uri

  let resultKey (key: string) =
    match key.Split([|'.'|]) with
    | [|_; key|] -> sprintf "%s.%s" Constant.Queue.Result key
    | _ -> Constant.Queue.Result

  member private __.ReceiveAssembies(args: BasicDeliverEventArgs) =
    base.LoadAssemblies(fun () -> args.Body)

  override this.ConsumeAssemblies() =
    channel.ExchangeDeclare(Constant.Exchange, Constant.Topic)
    let queueName = channel.QueueDeclare(Constant.Queue.Assemblies, false, false, false, null).QueueName
    channel.QueueBind(queueName, Constant.Exchange, sprintf "%s.*" Constant.Queue.Assemblies)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(this.ReceiveAssembies)
    channel.BasicConsume(queueName, true, consumer) |> ignore

  member private __.ReceiveTest (args: BasicDeliverEventArgs) =
    base.RunTest(fun () -> args.Body)
    |> Publisher.publish publisher Constant.Queue.Result (resultKey args.RoutingKey)
    channel.BasicAck(args.DeliveryTag, false)

  override this.ConsumeTest () =
    channel.BasicQos(0u, 1us, false)
    channel.ExchangeDeclare(Constant.Exchange, Constant.Topic)
    let queueName = channel.QueueDeclare(Constant.Queue.TestCase, false, false, false, null).QueueName
    channel.QueueBind(queueName, Constant.Exchange, sprintf "%s.*" Constant.Queue.TestCase)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(this.ReceiveTest)
    channel.BasicConsume(queueName, false, consumer) |> ignore

  override __.Dispose() =
    base.Dispose()
    publisher.Dispose()
    channel.Dispose()
    connection.Dispose()
