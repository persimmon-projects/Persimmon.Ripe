namespace Persimmon.Ripe.AzureServiceBus

open System
open System.IO
open System.Collections.Concurrent
open Microsoft.Azure
open Microsoft.ServiceBus
open Microsoft.ServiceBus.Messaging
open Nessos.Vagabond
open Persimmon
open Persimmon.ActivePatterns
open Persimmon.Ripe

type ResultCollector
  (
  connectionString: string,
  vmanager: VagabondManager,
  report: ITestResult -> unit,
  keyString: string,
  testCount: int) =

  let subscriptionName = sprintf "%s%s" Constant.Subscription.Result keyString

  let namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString)

  do
    if not <| namespaceManager.TopicExists(Constant.Topic) then
      namespaceManager.CreateTopic(Constant.Topic) |> ignore

  let publisher = new Publisher(connectionString, vmanager)

  let results = ConcurrentBag<ITestResult>()

  let fakeReporter = TextWriter.Null

  do
    vmanager.ComputeObjectDependencies(typeof<Result>, permitCompilation = true)
    |> vmanager.LoadVagabondAssemblies
    |> ignore

    if not <| namespaceManager.SubscriptionExists(Constant.Topic, subscriptionName) then
      let filter = SqlFilter(sprintf "%s = %s" Constant.Key.Result keyString)
      namespaceManager.CreateSubscription(Constant.Topic, subscriptionName, filter)
      |> ignore

  let addOrRetry = function
  | Success result ->
    let result = result :?> ITestResult
    report result
    results.Add(result)
  | Failure(v, e) ->
    match vmanager.Serializer.UnPickle<Test>(v) with
    | { Retry = 0; Run = f } ->
      let result = f fakeReporter :?> ITestResult
      report result
      results.Add(result)
    | t ->
      { t with Retry = t.Retry - 1 }
      |> Publisher.publish publisher Constant.Key.TestCase keyString

  let resultClient =
    SubscriptionClient.CreateFromConnectionString(connectionString, Constant.Topic, subscriptionName)

  let receive (msg: BrokeredMessage) =
    try
      vmanager.Serializer.UnPickle<Result>(msg.GetBody<byte []>())
      |> addOrRetry
      msg.Complete()
    with e ->
      printfn "%A" e
      msg.Abandon()

  // rename
  member __.Results =
    if results.Count = testCount then
      Complete(results)
    else Incomplete

  member __.StartConsume() =
    let options = OnMessageOptions(AutoComplete = false)
    resultClient.OnMessage(Action<BrokeredMessage>(receive), options)
