module Persimmon.Ripe.Config

open System

type RabbitMQ = {
  Uri: string
  UserName: string
  Password: string
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RabbitMQ =

  [<Literal>]
  let Exchange = "persimmon_ripe"

  [<Literal>]
  let Topic = "topic"

  module Queue =

    [<Literal>]
    let TestCase = "testcase"

    [<Literal>]
    let Result = "result"
