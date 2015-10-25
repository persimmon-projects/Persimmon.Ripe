namespace global

open System
open System.IO

type Args = {
  Inputs: FileInfo list
  Output: FileInfo
  NoProgress: bool
  Timeout: int
  RetryCount: int
  Help: bool
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Args =
  let empty = {
    Inputs = []
    Output = FileInfo(@".\report.xml")
    NoProgress = false
    Timeout = 600000
    RetryCount = 3
    Help = false
  }

  let private (|StartsWith|_|) (prefix: string) (target: string) =
    if target.StartsWith(prefix) then
      Some (target.Substring(prefix.Length))
    else
      None

  let private (|Split2By|_|) (separator: string) (target: string) =
    match target.Split([| separator |], 2, StringSplitOptions.None) with
    | [| s1; s2 |] -> Some (s1, s2)
    | _ -> None

  let private toFileInfoList (str: string) =
    str.Split(',') |> Array.map (fun path -> FileInfo(path)) |> Array.toList

  let rec parse acc = function
  | [] -> acc
  | "--no-progress"::rest -> parse { acc with NoProgress = true } rest
  | "--help"::rest -> parse { acc with Help = true } rest
  | (StartsWith "--" (Split2By ":" (key, value)))::rest ->
      match key with
      | "output" -> parse { acc with Output = FileInfo(value) } rest
      | "inputs" -> parse { acc with Inputs = acc.Inputs @ toFileInfoList value } rest
      | "timeout" -> parse { acc with Timeout = Int32.Parse(value) } rest
      | "retry" -> parse { acc with RetryCount = Int32.Parse(value) } rest
      | other -> failwithf "unknown option: %s" other
  | other::rest -> parse { acc with Inputs = (FileInfo(other))::acc.Inputs } rest

  let help =
    """usage: Persimmon.Ripe.Console.exe <options> <input>...

==== option ====
--output:<file>
    config the output file to print the result.
--inputs:<files>
    comma separated input files.
--no-progress
    disabled the report of progress.
--timeout:<ms>
    config test execution timeout.
--retry:<times>
    config test retry count.
--help
    print this help message.
================
"""