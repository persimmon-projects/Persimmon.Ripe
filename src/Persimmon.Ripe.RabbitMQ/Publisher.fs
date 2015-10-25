namespace Persimmon.Ripe.RabbitMQ

open System
open RabbitMQ.Client
open RabbitMQ.Client.Events
open Nessos.Vagabond

type Publisher(config: Config, vmanager: VagabondManager) =
  
  let connection = Connection.create config
  let channel = Connection.createChannel connection

  member __.Publish(queue, key, body) =
    channel.ExchangeDeclare(Constant.Exchange, Constant.Topic)
    channel.QueueDeclare(queue, false, false, false, null) |> ignore
    channel.BasicPublish(Constant.Exchange, key, null, vmanager.Serializer.Pickle(body))

  member __.Dispose() =
    channel.Dispose()
    connection.Dispose()

  interface IDisposable with
    member this.Dispose() = this.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Publisher =

  let publish (publisher: Publisher) queue key body = publisher.Publish(queue, key, body)
