namespace Persimmon.Ripe

open System
open Persimmon

type Result =
  | Success of Guid * ITestResult
  | Failure of byte[] * exn

type CollectResult =
  | Complete of ITestResult seq
  | Incomplete
