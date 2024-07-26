namespace PingApi
#nowarn "20"
open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open OpenTelemetry
open OpenTelemetry.Trace
open OpenTelemetry.Metrics
open OpenTelemetry.Exporter
open OpenTelemetry.Logs
open Npgsql
open System.Diagnostics
open System.Diagnostics.Metrics
open OpenTelemetry.Resources
open System.Collections.Generic
open Microsoft.AspNetCore.Http

module DiagnosticsConfig =

    [<Literal>]
    let ServiceName = "pingapi"
    [<Literal>]
    let ServiceNamespace = "pingapi"

    [<Literal>]
    let MyMeterOne = "MyMeterOne"

    type Instance() =
        static member ActivitySource = new ActivitySource(ServiceName)
        static member MyMeter = new Meter(MyMeterOne, "1.0")

module Env =
    let throwIfNull envVarName =
        let v = Environment.GetEnvironmentVariable(envVarName)

        if v = null then
            raise (Exception(envVarName + " is null"))
        else
            v
    
    let MAINDB_USER = lazy (throwIfNull "MAINDB_USER")
    let MAINDB_PASSWORD = lazy (throwIfNull "MAINDB_PASSWORD")
    let ENVNAME = lazy (throwIfNull "ENVNAME")
    let IMAGE_TAG = lazy (throwIfNull "IMAGE_TAG")
    let OTLP_ENDPOINT = lazy (throwIfNull "OTLP_ENDPOINT" |> Uri)

    let getConnectionString () =
        let hostFromEnv = Environment.GetEnvironmentVariable("HOST")
        let host = if hostFromEnv <> null then hostFromEnv else "maindb"
        let connectionString =
            sprintf "Host=%s;Port=5432;User ID=%s;Password=%s;Database=pingapp" host MAINDB_USER.Value MAINDB_PASSWORD.Value
        connectionString

module PingEndpoint =
    type PingResponse = { value: string }
    
    type PingServiceError =
        | DbRequestFailed of exn
        | OtherError of exn

    let pingService (logger: ILogger) (counter: Counter<int>): Result<PingResponse, PingServiceError> =
        use activity = DiagnosticsConfig.Instance.ActivitySource.StartActivity("Ping")
        activity.SetTag("foo", 1)
        activity.SetTag("bar", "Hello, World!")

        counter.Add(1)

        task {
            try
                use conn = new NpgsqlConnection(Env.getConnectionString())
                do! conn.OpenAsync()

                use cmd = new NpgsqlCommand("SELECT Name FROM public.pingtable", conn)
                use! reader = cmd.ExecuteReaderAsync()
                let! canRead = reader.ReadAsync()

                if canRead then
                    let firstNameInTable = reader.GetString(0)
                    logger.LogInformation(firstNameInTable)
                else
                    logger.LogInformation("No data in table!")

                return { value = "pong" } |> Ok
            with
                | ex -> 
                    logger.LogError(ex, "Error :(")
                    activity.RecordException(ex)
                    return ex |> OtherError |> Error
            
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously

    let pingEndpoint =
        fun (loggerFactory: ILoggerFactory) (counter: Counter<int>) ->
            fun () ->
                let logger = loggerFactory.CreateLogger("Ping")
                match pingService logger counter with
                | Ok ping -> 
                    Results.Json(ping)
                | Error err ->
                    match err with
                    | DbRequestFailed dbErr ->
                        Results.Problem(statusCode=500)
                    | OtherError otherErr ->
                        Results.Problem(statusCode=500)

module Program =

    let addService (rb: ResourceBuilder) =
        let envname = Env.ENVNAME.Value
        let serviceVersion = Env.IMAGE_TAG.Value

        rb.AddService(
            serviceName=DiagnosticsConfig.ServiceName,
            serviceNamespace=DiagnosticsConfig.ServiceNamespace,
            serviceVersion=serviceVersion,
            serviceInstanceId=Environment.MachineName)
        let attributes = Dictionary<string, Object>()
        attributes.Add("deployment.environment", box(envname))
        rb.AddAttributes(attributes)


    let getLoggerFactory () =
        let resourceBuilder = ResourceBuilder.CreateDefault() |> addService
        let otlpEndpoint = Env.OTLP_ENDPOINT.Value

        LoggerFactory.Create(fun builder ->
            builder
            |> fun b -> b.AddOpenTelemetry(fun logging ->
                logging.AddOtlpExporter(fun otlp ->
                    otlp.Endpoint <- otlpEndpoint
                    otlp.Protocol <- OtlpExportProtocol.Grpc
                    )
                |> fun l -> l.SetResourceBuilder(resourceBuilder)
                |> ignore)
            |> ignore)

    let startup (args: string[]) =
        let otlpEndpoint = Env.OTLP_ENDPOINT.Value

        let builder = WebApplication.CreateBuilder(args)
        
        let configureResourceBuilder = Action<ResourceBuilder>(fun rb ->
            addService rb
            rb |> ignore)

        builder.Services.AddOpenTelemetry()
        |> fun b -> b.ConfigureResource(configureResourceBuilder)
        |> fun b -> b.WithMetrics(fun metrics ->
            metrics
            |> fun m -> m.AddMeter(DiagnosticsConfig.MyMeterOne)
            |> fun m -> m.AddAspNetCoreInstrumentation()
            |> ignore
        )
        |> fun b -> b.WithTracing(fun tracing ->
            tracing
            |> fun t -> t.AddSource(DiagnosticsConfig.Instance.ActivitySource.Name)
            |> fun t -> t.AddAspNetCoreInstrumentation()
            |> fun t -> t.AddNpgsql(fun x -> x ; x |> ignore)
            |> ignore
        )
        |> fun b -> b.WithLogging(fun logger ->
            logger |> ignore
        )
        |> fun b -> b.UseOtlpExporter(OtlpExportProtocol.Grpc, otlpEndpoint)

        builder.Services.AddControllers()
        builder.Build()

    [<EntryPoint>]
    let main args =

        let Counter = DiagnosticsConfig.Instance.MyMeter.CreateCounter("pingapp_num_requests_since_startup", "Number of requests since startup.")

        let app = startup args
        use loggerFactory = getLoggerFactory ()

        app.MapGet("/ping", Func<IResult>(PingEndpoint.pingEndpoint loggerFactory Counter)) |> ignore

        app.MapGet("/health", Func<string>(fun () -> "I'm good cheers")) |> ignore

        app.UseHttpsRedirection()

        app.UseAuthorization()
        app.MapControllers()

        app.Run()

        let exitCode = 0
        exitCode
