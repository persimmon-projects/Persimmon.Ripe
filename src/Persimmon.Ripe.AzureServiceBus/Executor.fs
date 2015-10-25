namespace Persimmon.Ripe.AzureServiceBus

open System
open System.IO
open Nessos.Vagabond
open Microsoft.ServiceBus
open Microsoft.ServiceBus.Messaging
open Persimmon
open Persimmon.ActivePatterns
open Persimmon.Ripe

type Executor(connectionString: string, vmanager: VagabondManager, writer: TextWriter) =

  let namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString)

  do
    if not <| namespaceManager.TopicExists(Constant.Topic) then
      namespaceManager.CreateTopic(Constant.Topic) |> ignore

  let publisher = new Publisher(connectionString, vmanager)

  do
    vmanager.ComputeObjectDependencies(typeof<Executor>, permitCompilation = true)
    |> vmanager.LoadVagabondAssemblies
    |> Seq.iter (fprintfn writer "%A")

    if not <| namespaceManager.SubscriptionExists(Constant.Topic, Constant.Subscription.Assemblies) then
      namespaceManager.CreateSubscription(Constant.Topic, Constant.Subscription.Assemblies)
      |> ignore

    if not <| namespaceManager.SubscriptionExists(Constant.Topic, Constant.Subscription.TestCase) then
      namespaceManager.CreateSubscription(Constant.Topic, Constant.Subscription.TestCase)
      |> ignore

  let assembliesClient =
    SubscriptionClient.CreateFromConnectionString(
      connectionString, Constant.Topic, Constant.Subscription.Assemblies, ReceiveMode.ReceiveAndDelete)

  let receiveAssemblies (msg: BrokeredMessage) =
    try
      vmanager.Serializer.UnPickle<VagabondAssembly []>(msg.GetBody<byte []>())
      |> vmanager.LoadVagabondAssemblies
      |> Seq.iter (fprintfn writer "%A")
    with e -> fprintfn writer "%A" e

  let subscribeAssemblies () =
    let options = OnMessageOptions(AutoComplete = true)
    assembliesClient.OnMessage(Action<BrokeredMessage>(receiveAssemblies), options)

  let testClient =
    SubscriptionClient.CreateFromConnectionString(connectionString, Constant.Topic, Constant.Subscription.TestCase)

  let getKey (msg: BrokeredMessage) = msg.Properties.[Constant.Key.TestCase] :?> string

  let receiveTest (msg: BrokeredMessage) =
    let result =
      let body = msg.GetBody<byte []>()
      try
        let t = vmanager.Serializer.UnPickle<Test>(body)
        Success(t.Run writer)
      with e -> Failure(body, e)
    result
    |> Publisher.publish publisher Constant.Key.Result (getKey msg)
    msg.Complete()

  let subscribeTest () =
    let options = OnMessageOptions(AutoComplete = false)
    testClient.OnMessage(Action<BrokeredMessage>(receiveTest), options)

  member __.StartConsume() =
    subscribeAssemblies ()
    subscribeTest ()
