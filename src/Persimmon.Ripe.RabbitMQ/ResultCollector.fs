namespace Persimmon.Ripe.RabbitMQ

open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.Vagabond
open Persimmon
open Persimmon.Ripe.Runner

type ResultCollector
  (
  config: Config,
  vmanager: VagabondManager,
  report: ITestResult -> unit,
  keyString: string,
  testCount: int) =
  inherit TestResultCollector<Config, Routing>(config, vmanager, report, keyString, testCount)

  let testCaseKey = sprintf "%s.%s" Constant.Queue.TestCase keyString

  let connection = Connection.create config
  let channel = Connection.createChannel connection
  let publisher = new Publisher(config, vmanager)

  member private __.Receive(args: BasicDeliverEventArgs) =
    try
      base.AddOrRetryF(
        (fun () -> args.Body),
        Publisher.publish publisher Constant.Queue.TestCase testCaseKey
      )
      channel.BasicAck(args.DeliveryTag, false)
    with e -> printfn "%A" e

  override this.ReceiveResult() =
    channel.BasicQos(0u, 1us, false)
    channel.ExchangeDeclare(Constant.Exchange, Constant.Topic)
    let queueName = channel.QueueDeclare(Constant.Queue.Result, false, false, false, null).QueueName
    channel.QueueBind(queueName, Constant.Exchange, sprintf "%s.%s" Constant.Queue.Result keyString)
    let consumer = EventingBasicConsumer(channel)
    consumer.Received.Add(this.Receive)
    channel.BasicConsume(queueName, false, consumer) |> ignore

  override __.Dispose() =
    base.Dispose()
    channel.Dispose()
    connection.Dispose()
    publisher.Dispose()
