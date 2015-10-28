open System
open System.IO
open Persimmon.Ripe.AzureServiceBus
open Microsoft.Azure
open Nessos.Vagabond

[<EntryPoint>]
let main argv =
  let vmanager = Vagabond.Initialize(".")
  let connectionString =
    CloudConfigurationManager.GetSetting("Microsoft.ServiceBus.ConnectionString")
  use executor = new Executor(connectionString, vmanager, Console.Out)
  executor.StartConsume()
  while true do ()
  0
