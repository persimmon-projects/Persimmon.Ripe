namespace Persimmon.Ripe.RabbitMQ

open System
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.Vagabond
open Persimmon.Ripe.Runner

type Routing = {
  Queue: string
  Key: string
}

type Publisher(config: Config, vmanager: VagabondManager) =
  inherit TestPublisher<Config, Routing>(config, vmanager)
  
  let connection = Connection.create config
  let channel = Connection.createChannel connection

  override __.Publish(routing, value) =
    channel.ExchangeDeclare(Constant.Exchange, Constant.Topic)
    channel.QueueDeclare(routing.Queue, false, false, false, null) |> ignore
    channel.BasicPublish(Constant.Exchange, routing.Key, null, value)

  override __.Dispose() =
    channel.Dispose()
    connection.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Publisher =

  let publish (publisher: Publisher) queue key value =
    publisher.PublishTest({ Queue = queue; Key = key }, value)
