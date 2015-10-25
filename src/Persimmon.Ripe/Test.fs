namespace Persimmon.Ripe

open System.IO

type Test = {
  Retry: int
  Run: TextWriter -> obj
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Test =

  open Persimmon
  open Persimmon.ActivePatterns

  let ofTestObject retryCount (o: TestObject) =

    let rec writeResult (writer: TextWriter) prefix = function
    | ContextResult ctx ->
      ctx.Children
      |> Seq.iter (writeResult writer (sprintf "%s%s." prefix ctx.Name))
    | EndMarker -> writer.WriteLine()
    | TestResult tr -> fprintfn writer "end test: %s%s" prefix tr.FullName

    let run x = fun writer ->
      match x with
      | Context ctx -> ctx.Run(writeResult writer (sprintf "%s." ctx.Name)) |> box
      | TestCase tc ->
        let result = tc.Run()
        writeResult writer "" result
        box result

    { Retry = retryCount; Run = run o }
