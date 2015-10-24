namespace Persimmon.Ripe

open System
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.Vagabond
open Config

type Publisher(config: RabbitMQ, vmanager: VagabondManager) =
  
  let connection = Connection.create config
  let channel = Connection.createChannel connection

  member __.Publish(queue, key, body) =
    channel.ExchangeDeclare(RabbitMQ.Exchange, RabbitMQ.Topic)
    channel.QueueDeclare(queue, false, false, false, null) |> ignore
    channel.BasicPublish(RabbitMQ.Exchange, key, null, vmanager.Serializer.Pickle(body))

  member __.Dispose() =
    channel.Dispose()
    connection.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Publisher =

  let publish (publisher: Publisher) queue key body = publisher.Publish(queue, key, body)
