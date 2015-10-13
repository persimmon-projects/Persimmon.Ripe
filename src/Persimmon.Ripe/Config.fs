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
