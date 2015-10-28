namespace Persimmon.Ripe.AzureServiceBus

open System
open System.IO
open Microsoft.Azure
open Microsoft.ServiceBus
open Microsoft.ServiceBus.Messaging
open Nessos.Vagabond
open Persimmon
open Persimmon.Ripe.Runner

type ResultCollector
  (
  connectionString: string,
  vmanager: VagabondManager,
  report: ITestResult -> unit,
  keyString: string,
  testCount: int) =
  inherit TestResultCollector<string, Routing>(connectionString, vmanager, report, keyString, testCount)

  let subscriptionName = sprintf "%s%s" Constant.Subscription.Result keyString

  let namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString)

  do
    if not <| namespaceManager.TopicExists(Constant.Topic) then
      namespaceManager.CreateTopic(Constant.Topic) |> ignore

  let publisher = new Publisher(connectionString, vmanager)

  do
    if not <| namespaceManager.SubscriptionExists(Constant.Topic, subscriptionName) then
      let filter = SqlFilter(sprintf "%s = %s" Constant.Key.Result keyString)
      namespaceManager.CreateSubscription(Constant.Topic, subscriptionName, filter)
      |> ignore

  let resultClient =
    SubscriptionClient.CreateFromConnectionString(connectionString, Constant.Topic, subscriptionName)

  member private __.Receive(msg: BrokeredMessage) =
    try
      base.AddOrRetryF(
        (fun () -> msg.GetBody<byte []>()),
        Publisher.publish publisher Constant.Key.TestCase keyString
      )
      msg.Complete()
    with e ->
      printfn "%A" e
      msg.Abandon()

  override this.ReceiveResult() =
    let options = OnMessageOptions(AutoComplete = false)
    resultClient.OnMessage(Action<BrokeredMessage>(this.Receive), options)
