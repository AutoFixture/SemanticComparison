#r @"build/tools/FAKE.Core/tools/FakeLib.dll"

open Fake
open Fake.AppVeyor
open Fake.DotNetCli
open Fake.Testing
open System
open System.Diagnostics;
open System.Text.RegularExpressions

let buildDir = getBuildParamOrDefault "BuildDir" "build"
let buildToolsDir = buildDir </> "tools"
let nuGetOutputFolder = buildDir </> "NuGetPackages"
let nuGetPackages = !! (nuGetOutputFolder </> "*.nupkg" )
                    // Skip symbol packages because NuGet publish symbols automatically when package is published
                    -- (nuGetOutputFolder </> "*.symbols.nupkg")
let solutionToBuild = "Src/SemanticComparison.sln"
let configuration = getBuildParamOrDefault "BuildConfiguration" "Release"
let bakFileExt = ".orig"
let repositoryUrlOnGitHubSsh = "git@github.com:AutoFixture/SemanticComparison.git"
let repositoryUrlOnGitHubHttps = "https://github.com/AutoFixture/SemanticComparison.git"

type BuildVersionCalculationSource = { major: int; minor: int; revision: int; preSuffix: string; 
                                       commitsNum: int; sha: string; buildNumber: int }
let getVersionSourceFromGit buildNumber =
    // The --fist-parent flag is required to correctly work for vNext branch.
    // Example of output for a release tag: v3.50.2-288-g64fd5c5b, for a prerelease tag: v3.50.2-alpha1-288-g64fd5c5b
    let desc = Git.CommandHelper.runSimpleGitCommand "" "describe --tags --long --abbrev=40 --first-parent --match=v*"

    // Previously repository contained a few broken tags like "v.3.21.1". They were removed, but could still exist
    // in forks. We handle them as well to not fail on such repositories.
    let result = Regex.Match(desc,
                             @"^v(\.)?(?<maj>\d+)\.(?<min>\d+)\.(?<rev>\d+)(?<pre>-\w+\d*)?-(?<num>\d+)-g(?<sha>[a-z0-9]+)$",
                             RegexOptions.IgnoreCase)
                      .Groups

    let getMatch (name:string) = result.[name].Value

    { major = getMatch "maj" |> int
      minor = getMatch "min" |> int
      revision = getMatch "rev" |> int
      preSuffix = getMatch "pre"
      commitsNum = getMatch "num" |> int
      sha = getMatch "sha"
      buildNumber = buildNumber
    }

type BuildVersionInfo = { assemblyVersion:string; fileVersion:string; infoVersion:string; nugetVersion:string; 
                          source: Option<BuildVersionCalculationSource> }
let calculateVersion source =
    let s = source
    let (major, minor, revision, preReleaseSuffix, commitsNum, sha, buildNumber) =
        (s.major, s.minor, s.revision, s.preSuffix, s.commitsNum, s.sha, s.buildNumber)

    let assemblyVersion = sprintf "%d.%d.0.0" major minor
    let fileVersion = sprintf "%d.%d.%d.%d" major minor revision buildNumber
    
    // If number of commits since last tag is greater than zero, we append another identifier with number of commits.
    // The produced version is larger than the last tag version.
    // If we are on a tag, we use version without modification.
    // Examples of output: 3.50.2.1, 3.50.2.215, 3.50.1-rc1.3, 3.50.1-rc3.35
    let nugetVersion = match commitsNum with
                       | 0 -> sprintf "%d.%d.%d%s" major minor revision preReleaseSuffix
                       | _ -> sprintf "%d.%d.%d%s.%d" major minor revision preReleaseSuffix commitsNum

    let infoVersion = match commitsNum with
                      | 0 -> nugetVersion
                      | _ -> sprintf "%s-%s" nugetVersion sha

    { assemblyVersion=assemblyVersion; fileVersion=fileVersion; infoVersion=infoVersion; nugetVersion=nugetVersion; 
      source = Some source }

// Calculate version that should be used for the build. Define globally as data might be required by multiple targets.
// Please never name the build parameter with version as "Version" - it might be consumed by the MSBuild, override 
// the defined properties and break some tasks (e.g. NuGet restore).
let mutable buildVersion = match getBuildParamOrDefault "BuildVersion" "git" with
                           | "git"       -> getBuildParamOrDefault "BuildNumber" "0"
                                            |> int
                                            |> getVersionSourceFromGit
                                            |> calculateVersion

                           | assemblyVer -> { assemblyVersion = assemblyVer
                                              fileVersion = getBuildParamOrDefault "BuildFileVersion" assemblyVer
                                              infoVersion = getBuildParamOrDefault "BuildInfoVersion" assemblyVer
                                              nugetVersion = getBuildParamOrDefault "BuildNugetVersion" assemblyVer
                                              source = None }

let setVNextBranchVersion vNextVersion =
    buildVersion <-
        match buildVersion.source with
        // Don't update version if it was explicitly specified
        | None                                -> buildVersion
        // Don't update version if tag with current major version is already present (e.g. rc is released)
        | Some s when s.major >= vNextVersion -> buildVersion
        | Some source                         -> 
            // The trick is the "git describe" command contains the --first-parent flag.
            // Because of that git matched the last release tag before the fork was created and calculated number
            // of commits since that release. We are perfectly fine, as this number will constantly increase only.
            // Set version to X.0.0-alpha.NUM, where X - major version, NUM - commits since last release before fork
            { source with major = vNextVersion
                          minor = 0
                          revision = 0
                          preSuffix = "-alpha" }
            |> calculateVersion

let mutable enableSourceLink = false

let runMsBuild target configuration properties =
    let verbosity = match getBuildParam "BuildVerbosity" |> toLower with
                    | "quiet" | "q"         -> Quiet
                    | "minimal" | "m"       -> Minimal
                    | "normal" | "n"        -> Normal
                    | "detailed" | "d"      -> Detailed
                    | "diagnostic" | "diag" -> Diagnostic
                    | _ -> Minimal

    let configProperty = match configuration with
                         | Some c -> [ "Configuration", c ]
                         | _ -> []

    let sourceLinkCreatePropertyValue = match enableSourceLink with
                                        | true  -> "true"
                                        | false -> "false"

    let properties = configProperty @ properties
                     @ [ "AssemblyVersion", buildVersion.assemblyVersion
                         "FileVersion", buildVersion.fileVersion
                         "InformationalVersion", buildVersion.infoVersion
                         "PackageVersion", buildVersion.nugetVersion
                         "SourceLinkCreateOverride", sourceLinkCreatePropertyValue ]

    solutionToBuild
    |> build (fun p -> { p with MaxCpuCount = Some None
                                Verbosity = Some verbosity
                                Targets = [ target ]
                                Properties = properties })

let cleanBuild configuration = runMsBuild "Clean" (Some configuration) []
let rebuild configuration = runMsBuild "Rebuild" (Some configuration) []

Target "CleanAll"               DoNothing 
Target "CleanVerify"            (fun _ -> cleanBuild "Verify")
Target "CleanRelease"           (fun _ -> cleanBuild configuration)

Target "RestoreNuGetPackages" (fun _ -> runMsBuild "Restore" None [])

Target "EnableSourceLinkGeneration" (fun _ ->
    let areNotEqual a b = String.Equals(a, b, StringComparison.OrdinalIgnoreCase) |> not

    // A set of sanity checks to fail with meaningful errors.
    let originUrl = Git.CommandHelper.runSimpleGitCommand "" "config --get remote.origin.url"
    if ((areNotEqual originUrl repositoryUrlOnGitHubSsh) && (areNotEqual originUrl repositoryUrlOnGitHubHttps)) then
        failwithf 
            "Current repository has invalid git origin URL and will produce correct SourceLink info. Expected: '%s' or '%s'. Current: '%s'."
            repositoryUrlOnGitHubSsh
            repositoryUrlOnGitHubHttps
            originUrl
    
    let lineEndingConversion = Git.CommandHelper.runSimpleGitCommand "" "config --get core.autocrlf"
    if(areNotEqual lineEndingConversion "input") then
        failwithf "For correct SourceLink work git line conversion should be set to 'input'. Current: '%s'." lineEndingConversion

    enableSourceLink <- true
)

Target "Verify" (fun _ -> rebuild "Verify")

Target "BuildOnly" (fun _ -> rebuild configuration)
Target "TestOnly" (fun _ ->
    let findTestProjects pattern = System.IO.Directory.GetDirectories("Src", pattern)
    let getTestAssemblies framework projDirs =
        projDirs
        |> Seq.map (fun proj -> !! (sprintf "bin/%s/%s/*Test.dll" configuration framework)
                                |> SetBaseDir proj)
        |> Seq.collect id

    let xUnitTestProjects = findTestProjects "*Test" 

    xUnitTestProjects
    |> Seq.iter (fun projPath ->
        DotNetCli.RunCommand (fun p -> { p with WorkingDir = projPath })
                             (sprintf 
                                 "xunit --no-build --configuration %s"
                                 configuration)
    )
)

Target "Build" DoNothing
Target "Test"  DoNothing

Target "CleanNuGetPackages" (fun _ ->
    CleanDir nuGetOutputFolder
)

Target "NuGetPack" (fun _ ->
    // Pack projects using MSBuild
    runMsBuild "Pack" (Some configuration) [ "IncludeSource", "true"
                                             "IncludeSymbols", "true"
                                             "PackageOutputPath", FullName nuGetOutputFolder
                                             "NoBuild", "true" ]
)

let publishPackagesWithSymbols packageFeed symbolFeed accessKey =
    nuGetPackages
    |> Seq.map (fun pkg ->
        let meta = GetMetaDataFromPackageFile pkg
        meta.Id, meta.Version
    )
    |> Seq.iter (fun (id, version) -> NuGetPublish (fun p -> { p with Project = id
                                                                      Version = version
                                                                      OutputPath = nuGetOutputFolder
                                                                      PublishUrl = packageFeed
                                                                      AccessKey = accessKey
                                                                      SymbolPublishUrl = symbolFeed
                                                                      SymbolAccessKey = accessKey
                                                                      WorkingDir = nuGetOutputFolder
                                                                      ToolPath = buildToolsDir </> "nuget.exe" }))

Target "PublishNuGetPublic" (fun _ ->
    let feed = "https://www.nuget.org/api/v2/package"
    let key = getBuildParam "NuGetPublicKey"

    publishPackagesWithSymbols feed "" key
)

Target "PublishNuGetPrivate" (fun _ ->
    let packageFeed = "https://www.myget.org/F/autofixture/api/v2/package"
    let symbolFeed = "https://www.myget.org/F/autofixture/symbols/api/v2/package"
    let key = getBuildParam "NuGetPrivateKey"

    publishPackagesWithSymbols packageFeed symbolFeed key
)

Target "CompleteBuild"   DoNothing
Target "PublishNuGetAll" DoNothing

"CleanVerify"  ==> "CleanAll"
"CleanRelease" ==> "CleanAll"

"CleanAll"                   ==> "Verify"
"RestoreNuGetPackages"       ==> "Verify"
"EnableSourceLinkGeneration" ?=> "Verify"

"Verify"                             ==> "Build"
"BuildOnly"                          ==> "Build"

"BuildOnly"              ==> "TestOnly"

"Build"    ==> "Test"
"TestOnly" ==> "Test"

"CleanNuGetPackages" ==> "NuGetPack"
"Test"               ==> "NuGetPack"

"NuGetPack" ==> "CompleteBuild"

"NuGetPack"                  ==> "PublishNuGetPublic"
"EnableSourceLinkGeneration" ==> "PublishNuGetPublic"

"NuGetPack"                  ==> "PublishNuGetPrivate"
"EnableSourceLinkGeneration" ==> "PublishNuGetPrivate"

"PublishNuGetPublic"  ==> "PublishNuGetAll"
"PublishNuGetPrivate" ==> "PublishNuGetAll"

// ==============================================
// ================== AppVeyor ==================
// ==============================================

// Add helper to identify whether current trigger is PR
type AppVeyorEnvironment with
    static member IsPullRequest = isNotNullOrEmpty AppVeyorEnvironment.PullRequestNumber

type AppVeyorTrigger = SemVerTag | CustomTag | PR | VNextBranch | Unknown
let anAppVeyorTrigger =
    let tag = if AppVeyorEnvironment.RepoTag then Some AppVeyorEnvironment.RepoTagName else None
    let isPR = AppVeyorEnvironment.IsPullRequest
    let branch = if isNotNullOrEmpty AppVeyorEnvironment.RepoBranch then Some AppVeyorEnvironment.RepoBranch else None

    match tag, isPR, branch with
    | Some t, _, _ when "v\d+.*" >** t -> SemVerTag
    | Some _, _, _                     -> CustomTag
    | _, true, _                       -> PR
    // Branch name should be checked after the PR flag, because for PR it's set to the upstream branch name.
    | _, _, Some br when "v\d+" >** br -> VNextBranch
    | _                                -> Unknown

// Print state info at the very beginning
if buildServer = BuildServer.AppVeyor 
   then logfn "[AppVeyor state] Is tag: %b, tag name: '%s', is PR: %b, branch name: '%s', trigger: %A"
              AppVeyorEnvironment.RepoTag 
              AppVeyorEnvironment.RepoTagName 
              AppVeyorEnvironment.IsPullRequest
              AppVeyorEnvironment.RepoBranch
              anAppVeyorTrigger

Target "AppVeyor_SetVNextVersion" (fun _ ->
    // vNext branch has the following name: "vX", where X is the next version
    AppVeyorEnvironment.RepoBranch.Substring(1) 
    |> int
    |> setVNextBranchVersion
)

Target "AppVeyor" (fun _ ->
    //Artifacts might be deployable, so we update build version to find them later by file version
    if not AppVeyorEnvironment.IsPullRequest then UpdateBuildVersion buildVersion.fileVersion
)

"AppVeyor_SetVNextVersion" =?> ("Verify", anAppVeyorTrigger = VNextBranch)

// Add logic to resolve action based on current trigger info
dependency "AppVeyor" <| match anAppVeyorTrigger with
                         | SemVerTag                -> "PublishNuGetPublic"
                         | VNextBranch              -> "PublishNuGetPrivate"
                         | PR | CustomTag | Unknown -> "CompleteBuild"
"EnableSourceLinkGeneration" ==> "AppVeyor"

// ========= ENTRY POINT =========
RunTargetOrDefault "CompleteBuild"
