namespace Persimmon.Ripe.AzureServiceBus

open System
open Nessos.Vagabond
open Microsoft.ServiceBus
open Microsoft.ServiceBus.Messaging

type Publisher(connectionString: string, vmanager: VagabondManager) =
 
  let namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString)

  do
    if not <| namespaceManager.TopicExists(Constant.Topic) then
      namespaceManager.CreateTopic(Constant.Topic) |> ignore

  let client = TopicClient.CreateFromConnectionString(connectionString, Constant.Topic)
  
  member __.Publish(keyName, key: string, value) =
    use msg = new BrokeredMessage(vmanager.Serializer.Pickle(value))
    msg.Properties.[keyName] <- key
    client.Send(msg)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Publisher =

  let publish (publisher: Publisher) keyName key value = publisher.Publish(keyName, key, value)
