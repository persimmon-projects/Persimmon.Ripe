open System
open System.IO
open System.Text
open System.Reflection
open System.Diagnostics
open Persimmon
open Persimmon.ActivePatterns
open Persimmon.Runner
open Persimmon.Output
open Persimmon.Ripe
open Persimmon.Ripe.RabbitMQ
open FsYaml
open Nessos.Vagabond

let loadTests retry (files: FileInfo list) =
  let asms = files |> List.map (fun f ->
    let assemblyRef = AssemblyName.GetAssemblyName(f.FullName)
    Assembly.Load(assemblyRef))
  TestCollector.collectRootTestObjects asms
  |> List.map (Test.ofTestObject retry)

let collectResult (watch: Stopwatch) (reporter: Reporter) (consoleReporter: Reporter) rc =
  let rec inner (collector: ResultCollector) = async {
    match collector.Results with
    | Incomplete -> return! inner collector
    | Complete res ->
      watch.Stop()
      let errors = Seq.sumBy TestRunner.countErrors res
      reporter.ReportProgress(TestResult.endMarker)
      reporter.ReportSummary(res)
      consoleReporter.ReportSummary(res)
      return errors
  }
  inner rc

let entryPoint (args: Args) =
  
  let watch = Stopwatch()
  
  use progress = if args.NoProgress then TextWriter.Null else Console.Out
  use output = new StreamWriter(args.Output.FullName, false, Encoding.UTF8) :> TextWriter
  use error = Console.Error
  let formatter = Formatter.XmlFormatter.junitStyle watch

  let config = Yaml.load<Config> <| File.ReadAllText(args.RemoteConfig.FullName)

  use reporter =
    new Reporter(
      new Printer<_>(progress, Formatter.ProgressFormatter.dot),
      new Printer<_>(output, formatter),
      new Printer<_>(error, Formatter.ErrorFormatter.normal))

  use consoleReporter =
    new Reporter(
      new Printer<_>(TextWriter.Null, Formatter.ProgressFormatter.dot),
      new Printer<_>(Console.Out, Formatter.SummaryFormatter.normal watch),
      new Printer<_>(TextWriter.Null, Formatter.ErrorFormatter.normal))

  if args.Help then
    error.WriteLine(Args.help)

  let founds, notFounds = args.Inputs |> List.partition (fun file -> file.Exists)
  if founds |> List.isEmpty then
    reporter.ReportError("input is empty.")
    -1
  elif notFounds |> List.isEmpty then
       
    let tests = loadTests args.RetryCount founds

    let key = Guid.NewGuid()
    let keyString = key.ToString()
    let testCaseKey = sprintf "%s.%s" Constant.Queue.TestCase keyString
    let asmsKey = sprintf "%s.%s" Constant.Queue.Assemblies keyString
    
    let vmanager = Vagabond.Initialize(".")
    let asms = vmanager.ComputeObjectDependencies(tests, permitCompilation = true)
    vmanager.LoadVagabondAssemblies(asms) |> ignore
    
    use publisher = new Publisher(config, vmanager)
    Publisher.publish publisher Constant.Queue.Assemblies asmsKey asms
    
    use collector = new ResultCollector(config, vmanager, reporter.ReportProgress, keyString, Seq.length tests)
    collector.StartConsume()
    
    let result =
      try
        let r =
          async {
            do! Async.Sleep(100)
            watch.Start()
            tests |> List.iter (Publisher.publish publisher Constant.Queue.TestCase testCaseKey)
            return! collectResult watch reporter consoleReporter collector
          }
          |> Async.Catch
        Async.RunSynchronously(r, args.Timeout)
      with e -> Choice2Of2 e
    match result with
    | Choice1Of2 v -> v
    | Choice2Of2 e ->
      reporter.ReportError(sprintf "FATAL ERROR: %A" e)
      -3
  else
    reporter.ReportError("file not found: " + (String.Join(", ", notFounds)))
    -2

type FailedCounter () =
  inherit MarshalByRefObject()
  
  member val Failed = 0 with get, set

[<Serializable>]
type Callback (args: Args, body: Args -> int, failed: FailedCounter) =
  member __.Run() =
    failed.Failed <- body args

let run act =
  let info = AppDomain.CurrentDomain.SetupInformation
  let appDomain = AppDomain.CreateDomain("persimmon ripe console domain", null, info)
  try
    appDomain.DoCallBack(act)
  finally
    AppDomain.Unload(appDomain)

[<EntryPoint>]
let main argv = 
  let args = Args.parse Args.empty (argv |> Array.toList)
  let failed = FailedCounter()
  let callback = Callback(args, entryPoint, failed)
  run (CrossAppDomainDelegate(callback.Run))
  failed.Failed
