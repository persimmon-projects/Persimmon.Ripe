module Persimmon.Ripe.Connection

open RabbitMQ.Client
open Config

let create config =
  let factory = ConnectionFactory(uri = config.Uri, UserName = config.UserName, Password = config.Password)
  factory.CreateConnection()

let createChannel (connection: IConnection) = connection.CreateModel()
