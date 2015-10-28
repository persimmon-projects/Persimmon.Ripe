module Persimmon.Ripe.Runner.TestCollector

open System.Reflection
open Persimmon.Runner
open Persimmon.Ripe

let collectTests retry (asms: Assembly list) =
  TestCollector.collectRootTestObjects asms
  |> List.map (Test.ofTestObject retry)
