module Persimmon.Ripe.Config

open System

type RabbitMQ = {
  Uri: Uri
  UserName: string
  Password: string
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RabbitMQ =

  [<Literal>]
  let Exchange = "persimmon_ripe"

  let parse uri name password =
    match Uri.TryCreate(uri, UriKind.Absolute) with
    | true, uri ->
      Some {
        Uri = uri
        UserName = name
        Password = password
      }
    | false, _ -> None
