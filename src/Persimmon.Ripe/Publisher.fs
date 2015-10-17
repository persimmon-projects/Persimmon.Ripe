namespace Persimmon.Ripe

open System
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.FsPickler
open Config

type Publisher(config: RabbitMQ) =
  
  let connection = Connection.create config
  let channel = Connection.createChannel connection
  let serializer = FsPickler.CreateBinarySerializer()

  member __.Publish(key, body) =
    channel.ExchangeDeclare(RabbitMQ.Exchange, "topic")
    channel.QueueDeclare() |> ignore
    channel.BasicPublish(RabbitMQ.Exchange, key, null, serializer.Pickle(body))

  member __.Dispose() =
    channel.Dispose()
    connection.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Publisher =

  let publish (publisher: Publisher) key body = publisher.Publish(key, body)
