namespace Persimmon.Ripe

open System
open System.IO
open Persimmon

type Result =
  | Success of obj
  | Failure of byte[] * exn

type CollectResult =
  | Complete of ITestResult seq
  | Incomplete

type Test = {
  Retry: int
  Run: TextWriter -> obj
}
