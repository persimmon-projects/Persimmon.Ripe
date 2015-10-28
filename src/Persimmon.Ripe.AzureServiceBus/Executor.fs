namespace Persimmon.Ripe.AzureServiceBus

open System
open System.IO
open Nessos.Vagabond
open Microsoft.ServiceBus
open Microsoft.ServiceBus.Messaging
open Persimmon.Ripe
open Persimmon.Ripe.Runner

type Executor(connectionString: string, vmanager: VagabondManager, writer: TextWriter) =
  inherit TestExecutor<string, Routing>(connectionString, vmanager, writer)

  let namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString)

  do
    if not <| namespaceManager.TopicExists(Constant.Topic) then
      namespaceManager.CreateTopic(Constant.Topic) |> ignore

  let publisher = new Publisher(connectionString, vmanager)

  do
    if not <| namespaceManager.SubscriptionExists(Constant.Topic, Constant.Subscription.Assemblies) then
      namespaceManager.CreateSubscription(Constant.Topic, Constant.Subscription.Assemblies)
      |> ignore

    if not <| namespaceManager.SubscriptionExists(Constant.Topic, Constant.Subscription.TestCase) then
      namespaceManager.CreateSubscription(Constant.Topic, Constant.Subscription.TestCase)
      |> ignore

  let assembliesClient =
    SubscriptionClient.CreateFromConnectionString(
      connectionString, Constant.Topic, Constant.Subscription.Assemblies, ReceiveMode.ReceiveAndDelete)

  let testClient =
    SubscriptionClient.CreateFromConnectionString(connectionString, Constant.Topic, Constant.Subscription.TestCase)

  let getKey (msg: BrokeredMessage) = msg.Properties.[Constant.Key.TestCase] :?> string

  member private __.ReceiveAssemblies(msg: BrokeredMessage) =
    base.LoadAssemblies(fun () -> msg.GetBody<byte []>())

  override this.ConsumeAssemblies() =
    let options = OnMessageOptions(AutoComplete = true)
    assembliesClient.OnMessage(Action<BrokeredMessage>(this.ReceiveAssemblies), options)

  member private __.ReceiveTest(msg: BrokeredMessage) =
    base.RunTest(fun () -> msg.GetBody<byte []>())
    |> Publisher.publish publisher Constant.Key.Result (getKey msg)
    msg.Complete()

  override this.ConsumeTest() =
    let options = OnMessageOptions(AutoComplete = false)
    testClient.OnMessage(Action<BrokeredMessage>(this.ReceiveTest), options)
