namespace Persimmon.Ripe.AzureServiceBus

open System
open Nessos.Vagabond
open Microsoft.ServiceBus
open Microsoft.ServiceBus.Messaging
open Persimmon.Ripe.Runner

type Routing = {
  Name: string
  Key: string
}

type Publisher(connectionString: string, vmanager: VagabondManager) =
  inherit TestPublisher<string, Routing>(connectionString, vmanager)
 
  let namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString)

  do
    if not <| namespaceManager.TopicExists(Constant.Topic) then
      namespaceManager.CreateTopic(Constant.Topic) |> ignore

  let client = TopicClient.CreateFromConnectionString(connectionString, Constant.Topic)
  
  override __.Publish(routingKey, value) =
    use msg = new BrokeredMessage(vmanager.Serializer.Pickle(value))
    msg.Properties.[routingKey.Name] <- routingKey.Key
    client.Send(msg)

  override __.Dispose() = ()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Publisher =

  let publish (publisher: Publisher) keyName key value =
    publisher.PublishTest({ Name = keyName; Key = key}, value)
