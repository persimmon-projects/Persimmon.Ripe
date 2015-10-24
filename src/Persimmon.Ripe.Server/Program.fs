open System
open System.IO
open Persimmon.Ripe
open FsYaml
open Nessos.Vagabond

[<EntryPoint>]
let main argv =
  let config = Yaml.load<Config.RabbitMQ> (File.ReadAllText(argv.[0]))
  let vmanager = Vagabond.Initialize(".")
  use executor = new Executor(config, vmanager, Console.Out)
  executor.StartConsume()
  while true do ()
  0
