module Persimmon.Ripe.RabbitMQ.Connection

open System
open RabbitMQ.Client

let create config =
  let uri = Uri(config.Uri)
  let factory = ConnectionFactory(uri = uri, UserName = config.UserName, Password = config.Password)
  factory.CreateConnection()

let createChannel (connection: IConnection) = connection.CreateModel()
