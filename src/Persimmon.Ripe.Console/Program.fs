open System
open System.IO
open System.Text
open System.Reflection
open System.Diagnostics
open Persimmon
open Persimmon.Runner
open Persimmon.Output
open Persimmon.Ripe
open FsYaml

let entryPoint (args: Args) =
  
  let guid = Guid.NewGuid()
  let watch = Stopwatch()
  
  use progress = if args.NoProgress then IO.TextWriter.Null else Console.Out
  use output = new StreamWriter(args.Output.FullName, false, Encoding.UTF8) :> TextWriter
  use error = Console.Error
  let formatter = Formatter.XmlFormatter.junitStyle watch

  let config = Yaml.load<Config.RabbitMQ> <| File.ReadAllText(args.RemoteConfig.FullName)

  use reporter =
    new Reporter(
      new Printer<_>(progress, Formatter.ProgressFormatter.dot),
      new Printer<_>(output, formatter),
      new Printer<_>(error, Formatter.ErrorFormatter.normal))

  let run rs =
    let rec inner (collector: ResultCollector) = async {
      match collector.Results with
      | Incomplete -> return! inner collector
      | Complete res ->
        watch.Stop()
        let errors = Seq.sumBy TestRunner.countErrors res
        let res = { TestRunner.Errors = errors; TestRunner.ExecutedRootTestResults = res }
        reporter.ReportProgress(TestResult.endMarker)
        reporter.ReportSummary(res.ExecutedRootTestResults)
        return res.Errors
    }
    watch.Start()
    inner rs |> Async.RunSynchronously

  if args.Help then
    error.WriteLine(Args.help)

  let founds, notFounds = args.Inputs |> List.partition (fun file -> file.Exists)
  if founds |> List.isEmpty then
    reporter.ReportError("input is empty.")
    -1
  elif notFounds |> List.isEmpty then
    let asms = founds |> List.map (fun f ->
      let assemblyRef = AssemblyName.GetAssemblyName(f.FullName)
      Assembly.Load(assemblyRef))
    // collect and run
    let tests =
      TestCollector.collectRootTestObjects asms
      |> Seq.map (fun x -> (Guid.NewGuid(), x))
    use collector = new ResultCollector(config, Map.ofSeq tests)
    collector.Connect()
    use publisher = new Publisher(config)
    tests |> Seq.iter (Publisher.publish publisher "testcase")
    run collector
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
