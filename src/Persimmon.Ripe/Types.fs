namespace Persimmon.Ripe

open System
open Persimmon

type Result =
  | Success of obj
  | Failure of byte[] * exn

type CollectResult =
  | Complete of ITestResult seq
  | Incomplete
