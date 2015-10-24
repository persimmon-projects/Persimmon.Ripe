open System
open System.IO
open Persimmon.Ripe
open FsYaml

[<EntryPoint>]
let main argv =
  let config = Yaml.load<Config.RabbitMQ> (File.ReadAllText(argv.[0]))
  use executor = new Executor(config, Console.Out)
  executor.StartConsume()
  while true do ()
  0
