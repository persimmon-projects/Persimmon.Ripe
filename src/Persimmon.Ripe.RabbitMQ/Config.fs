namespace Persimmon.Ripe.RabbitMQ

open System

type Config = {
  Uri: string
  UserName: string
  Password: string
}

module Constant =

  [<Literal>]
  let Exchange = "persimmon_ripe"

  [<Literal>]
  let Topic = "topic"

  module Queue =

    [<Literal>]
    let Assemblies = "assemblies"

    [<Literal>]
    let TestCase = "testcase"

    [<Literal>]
    let Result = "result"
