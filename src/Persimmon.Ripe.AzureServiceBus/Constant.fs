module Persimmon.Ripe.AzureServiceBus.Constant

[<Literal>]
let Topic = "PersimmonRipe"

module Key =

  [<Literal>]
  let Assemblies = "Assemblies"

  [<Literal>]
  let TestCase = "TestCase"

  [<Literal>]
  let Result = "Result"

module Subscription =

  [<Literal>]
  let Assemblies = "AssembliesMessages"

  [<Literal>]
  let TestCase = "TestCaseMessages"

  [<Literal>]
  let Result = "ResultMessages"
