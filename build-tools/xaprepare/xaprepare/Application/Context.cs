using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Xamarin.Android.Prepare
{
	/// <summary>
	///   Shared application contet.
	/// </summary>
	partial class Context : AppObject
	{
		const ConsoleColor BannerColor                        = ConsoleColor.DarkGreen;

		public const ConsoleColor SuccessColor                = ConsoleColor.Green;
		public const ConsoleColor FailureColor                = ConsoleColor.Red;
		public const ConsoleColor WarningColor                = ConsoleColor.Yellow;

		static readonly IEnumerable<string> XASolutionFilesPath         = new string [] {
			Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "Xamarin.Android.BootstrapTasks.sln"),
			Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "Xamarin.Android.Build.Tasks.sln"),
			Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "Xamarin.Android.sln"),
		};

		static readonly IEnumerable<string> XATestsSolutionFilesPath    = new string [] {
			Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "Xamarin.Android-Tests.sln"),
		};

		string? logDirectory;
		string mainLogFilePath;
		string? xaInstallPrefix;
		string? overridenLogDirectory;
		string? configuration;
		string productVersion;
		string androidLatestStableFrameworkVersion;
		string? hashAlgorithm;
		bool canOutputColor;
		bool canConsoleUseUnicode;
		Characters? characters;
		bool? useColor;
		bool? dullMode;
		Scenario? defaultScenario;
		HashSet<string>? hostJitAbis;
		HashSet<string>? targetAotAbis;
		HashSet<string>? targetJitAbis;
		List<RuleGenerator>? ruleGenerators;
		string? debugFileExtension;
		CompressionFormat? compressionFormat;
		Dictionary<KnownConditions, bool> conditions  = new Dictionary<KnownConditions, bool> ();

		/// <summary>
		///   Access the only instance of the Context class
		/// </summary>
		public static Context Instance                 { get; }

		/// <summary>
		///   This should not really be here, but due to the fact that Windows is still special-cased (until we
		///   can drop using the bundle and build everything on Windows) we need to check "dynamically" if we're
		///   running on Windows in some cases. This will be set to <c>true</c> only when xaprepare is built on
		///   Windows.
		/// </summary>
		public static bool IsWindows                   => isWindows;

		/// <summary>
		///   Information about the operating system we're currently running on. See <see cref="OS" />
		/// </summary>
		public OS OS                                   { get; private set; }

		/// <summary>
		///   A shortcut to access a small set of essential tools used by the bootstrapper. See <see cref="EssentialTools" />
		/// </summary>
		public EssentialTools Tools                    { get; private set; } = new EssentialTools ();

		/// <summary>
		///   Information about the current build. <see cref="BuildInfo" />
		/// </summary>
		public BuildInfo BuildInfo                     { get; private set; } = new BuildInfo ();

		/// <summary>
		///   All the scenarios known to the bootstrapper
		/// </summary>
		public IDictionary<string, Scenario> Scenarios { get; } = new SortedDictionary<string, Scenario> (StringComparer.OrdinalIgnoreCase);

		/// <summary>
		///   Default scenario to execute if none was specified by the user on the command line
		/// </summary>
		public Scenario DefaultScenario                => defaultScenario ?? throw new InvalidOperationException ("Default scenario not specified (was .Init called?)");

		/// <summary>
		///   Scenario selected for the current session
		/// </summary>
		public Scenario SelectedScenario               { get; private set; } = new ScenarioNoScenario ();

		/// <summary>
		///   Whether the current run of the bootstrapper can interact with the user or not
		/// </summary>
		public bool InteractiveSession                 { get; }

		/// <summary>
		///   Set of properties available in this instance of the bootstrapper. See <see cref="KnownProperties" /> and <see
		///   cref="Properties" />
		/// </summary>
		public Properties Properties                   { get; } = new Properties ();

		/// <summary>
		///   Time stamp of the current build
		/// </summary>
		public string BuildTimeStamp                   { get; }

		/// <summary>
		///   A collection of all the methods to obtain version numbers from programs. See <see cref="t:VersionFetchers" />
		/// </summary>
		public VersionFetchers VersionFetchers         { get; private set; } = new VersionFetchers ();


		/// <summary>
		///   Logging verbosity/level of the current session.
		/// </summary>
		public LoggingVerbosity LoggingVerbosity       { get; set; } = Configurables.Defaults.LoggingVerbosity;

		/// <summary>
		///   How many make/ninja jobs to run when building software
		/// </summary>
		public uint MakeConcurrency                    { get; set; } = Configurables.Defaults.MakeConcurrency;

		/// <summary>
		///   Do not use emoji characters
		/// </summary>
		public bool NoEmoji                            { get; set; } = !Configurables.Defaults.UseEmoji;

		/// <summary>
		///   Force a rebuild of the Mono runtimes
		/// </summary>
		public bool ForceRuntimesBuild                 { get; set; }

		/// <summary>
		///   Automatically provision all the missing programs
		/// </summary>
		public bool AutoProvision                      { get; set; }

		/// <summary>
		///   If a program being provisioned automatically requires administrative rights to install, use sudo
		/// </summary>
		public bool AutoProvisionUsesSudo              { get; set; }

		/// <summary>
		///   Do not terminate session when Mono is newer than specified in the dependencies
		/// </summary>
		public bool IgnoreMaxMonoVersion               { get; set; } = true;

		/// <summary>
		///   Do not terminate session when Mono is older than specified in the dependencies
		/// </summary>
		public bool IgnoreMinMonoVersion               { get; set; } = false;

		/// <summary>
		///   Current session execution mode. See <see cref="t:ExecutionMode" />
		/// </summary>
		public ExecutionMode ExecutionMode             { get; set; } = Configurables.Defaults.ExecutionMode;

		/// <summary>
		///   Set of Mono command line options to be placed in the `MONO_OPTIOS` environment variable
		/// </summary>
		public List<string> MonoOptions                { get; set; }

		/// <summary>
		///   Enable all supported targets, runtimes etc. Takes effect only if set before <see cref="Init"/> is called
		/// </summary>
		public bool EnableAllTargets                   { get; set; }

		/// <summary>
		///   Path to the current session's main log file
		/// </summary>
		public string MainLogFilePath                     => mainLogFilePath;

		/// <summary>
		///   Path to the Xamarin.Android solution file
		/// </summary>
		public IEnumerable<string> XASolutionFiles        => XASolutionFilesPath;

		/// <summary>
		///   Path to the Xamarin.Android tests solution file
		/// </summary>
		public IEnumerable<string> XATestsSolutionFiles   => XATestsSolutionFilesPath;

		/// <summary>
		///   If <c>true</c>, the current console is capable of displayig UTF-8 characters
		/// </summary>
		public bool CanConsoleUseUnicode                  => canConsoleUseUnicode;

		/// <summary>
		///   A set of various special characters used in progress messages. See <see cref="t:Characters" />
		/// </summary>
		public Characters Characters                      => characters ?? throw new InvalidOperationException ("Context not initialized properly (was .Init called?)");

		/// <summary>
		///   Xamarin.Android version
		/// </summary>
		public string ProductVersion                      => productVersion;

		/// <summary>
		///   If <c>true</c> make messages logged to console not use colors, do not use "fancy" progress indicators etc
		/// </summary>
		public bool DullMode {
			get => dullMode.HasValue ? dullMode.Value : ExecutionMode == ExecutionMode.CI;
			set => dullMode = value;
		}

		/// <summary>
		///   Compression format to use for the archives we create
		/// </summary>
		public CompressionFormat CompressionFormat {
			get => compressionFormat ?? Configurables.Defaults.DefaultCompressionFormat;
			set => compressionFormat = value;
		}

		/// <summary>
		///   Current session buuld configuration
		/// </summary>
		public string Configuration {
			get => configuration ?? Properties.GetRequiredValue (KnownProperties.Configuration);
			set {
				if (String.IsNullOrEmpty (value))
					throw new ArgumentException ("must not be null or empty", nameof (value));
				if (!String.IsNullOrEmpty (configuration))
					throw new InvalidOperationException ("Configuration can be set only once");

				logDirectory = null;
				configuration = value;
			}
		}

		/// <summary>
		///   Whether or not current build is a debug one.
		/// </summary>
		public bool IsDebugBuild => String.Compare (Configuration, "Debug", StringComparison.OrdinalIgnoreCase) == 0;

		/// <summary>
		///   Hash algorithm to use when calculating various hashes.
		/// </summary>
		public string HashAlgorithm {
			get => String.IsNullOrEmpty (hashAlgorithm) ? Configurables.Defaults.HashAlgorithm : HashAlgorithm;
			set {
				value = value.Trim ();
				if (String.IsNullOrEmpty (value))
					throw new ArgumentException ("must not be null or empty", "value");
				hashAlgorithm = value;
			}
		}

		/// <summary>
		///   Directoruy containing all the session logs
		/// </summary>
		public string LogDirectory {
			get => GetLogDirectory ();
			set {
				if (String.IsNullOrEmpty (value))
					throw new ArgumentException ("must not be null or empty", nameof (value));

				overridenLogDirectory = value;
			}
		}

		/// <summary>
		///   Whether or not log messages should use color
		/// </summary>
		public bool UseColor {
			get => canOutputColor && (!useColor.HasValue || useColor.Value);
			set => useColor = value;
		}

		/// <summary>
		///   Prefix where Xamarin.Android is installed
		/// </summary>
		public string XAInstallPrefix {
			get {
				if (String.IsNullOrEmpty (xaInstallPrefix)) {
					xaInstallPrefix = Properties.GetRequiredValue (KnownProperties.XAInstallPrefix);
					if (String.IsNullOrEmpty (xaInstallPrefix))
						throw new InvalidOperationException ("Xamarin.Android install prefix property has an empty value or is absent");
				}
				return xaInstallPrefix!;
			}
		}

		/// <summary>
		///   <c>true</c> if any Windows ABI targets are enabled
		/// </summary>
		public bool WindowsJitAbisEnabled {
			get => IsHostJitAbiEnabled (AbiNames.HostJit.Win32) || IsHostJitAbiEnabled (AbiNames.HostJit.Win64);
		}

		/// <summary>
		///   <c>true</c> if any Android device AOT targets are enabled
		/// </summary>
		public bool TargetAotAbisEnabled {
			get => AbiNames.AllTargetAotAbis.Any (abi => IsTargetAotAbiEnabled (abi));
		}

		/// <summary>
		///   A collection of delegates which can add rules to the `rules.mk` file generated at the end of
		///   bootstrapper's run
		/// </summary>
		public List<RuleGenerator> RuleGenerators {
			get {
				if (ruleGenerators == null)
					ruleGenerators = new List<RuleGenerator> ();
				return ruleGenerators;
			}
		}

		/// <summary>
		///   Extensions of files with debug information
		/// </summary>
		public string DebugFileExtension {
			get => debugFileExtension ?? Configurables.Defaults.DebugFileExtension;
			set {
				value = value.Trim ();
				if (String.IsNullOrEmpty (value))
					throw new ArgumentException ("must not be null or empty", nameof (value));
				if (value [0] != '.')
					debugFileExtension = $".{value}";
				else
					debugFileExtension = value;
			}
		}

		/// <summary>
		///   Collection of programs or dependencies which should be reinstalled.
		/// </summary>
		public RefreshableComponent ComponentsToRefresh { get; set; }

		/// <summary>
		/// Set by the --mono-archive-url flag
		/// </summary>
		public string MonoArchiveCustomUrl { get; set; } = String.Empty;

		/// <summary>
		///   Set by <see cref="Step_DownloadMonoArchive"/> if the archive has been downloaded and validated, so
		///   that the <see cref="Step_InstallMonoRuntimes"/> step doesn't attempt to rebuild Mono.
		/// </summary>
		public bool MonoArchiveDownloaded { get; set; }

		// <summary>
		///   Set by <see cref="Step_Get_Android_BuildTools"/> if the archive has been downloaded and validated.
		/// </summary>
		public bool BuildToolsArchiveDownloaded { get; set; }

		/// <summary>
		///   Determines whether or not we are running on a hosted azure pipelines agent.
		///   These agents have certain limitations, the most pressing being the amount of available storage.
		///   https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=azure-devops#capabilities-and-limitations.
		/// </summary>
		public bool IsRunningOnHostedAzureAgent {
			get {
				string? agentNameValue = Environment.GetEnvironmentVariable ("AGENT_NAME");
				bool hasHostedAgentName = !string.IsNullOrEmpty (agentNameValue) && agentNameValue.ToUpperInvariant ().Contains ("AZURE PIPELINES");
				string? serverTypeValue = Environment.GetEnvironmentVariable ("SYSTEM_SERVERTYPE");
				bool isHostedServerType = !string.IsNullOrEmpty (serverTypeValue) && serverTypeValue.ToUpperInvariant ().Contains ("HOSTED");
				return hasHostedAgentName || isHostedServerType;
			}
		}

		/// <summary>
		///   Do not install mingw-w64 with brew on MacOS, default false
		/// </summary>
		public bool NoMingwW64 { get; set; } = false;

		static Context ()
		{
			Instance = new Context ();
		}

		Context ()
		{
			try {
				// This may throw on Windows
				Console.CursorVisible = false;
			} catch (IOException) {
				// Ignore
			}

			OS = new NoOS (this);
			MonoOptions = new List<string> {
				"--debug", // Doesn't hurt to have line numbers in stack traces...
			};

			// Standard Console class offers no way to detect if the terminal can use color, so we use this rather poor
			// way to detect it
			canOutputColor = true;
			try {
				ConsoleColor color = Console.ForegroundColor;
			} catch (IOException) {
				canOutputColor = false;
			}

			Properties.PropertiesChanged += PropertiesChanged;
			if (Console.OutputEncoding.EncodingName.IndexOf ("Unicode", StringComparison.OrdinalIgnoreCase) >= 0) {
				canConsoleUseUnicode = true;
			} else {
				canConsoleUseUnicode =
					Console.OutputEncoding is UTF7Encoding ||
					Console.OutputEncoding is UTF8Encoding ||
					Console.OutputEncoding is UTF32Encoding ||
					Console.OutputEncoding is UnicodeEncoding;
			}

			Log.Todo ("better checks for interactive session (isatty?)");
			InteractiveSession = !Console.IsOutputRedirected;

			var now = DateTime.Now;
			BuildTimeStamp = $"{now.Year}{now.Month:00}{now.Day:00}T{now.Hour:00}{now.Minute:00}{now.Second:00}";
			mainLogFilePath = GetLogFilePath (null, true);
			Log.Instance.SetLogFile (mainLogFilePath);

			productVersion = Properties.GetRequiredValue (KnownProperties.ProductVersion);
			androidLatestStableFrameworkVersion = Properties.GetRequiredValue (KnownProperties.AndroidLatestStableFrameworkVersion);

			Log.Instance.DebugLine ("All defined properties:");
			foreach (KeyValuePair<string, string> prop in Properties) {
				Log.Instance.DebugLine ($"  {prop.Key} = {prop.Value}");
			}
		}

		void PropertiesChanged (object? sender, PropertiesChangedEventArgs args)
		{
			if (String.Compare (KnownProperties.AndroidSupportedTargetJitAbis, args.Name, StringComparison.Ordinal) == 0) {
				targetJitAbis = null;
				return;
			}

			if (String.Compare (KnownProperties.AndroidSupportedHostJitAbis, args.Name, StringComparison.Ordinal) == 0) {
				hostJitAbis = null;
				return;
			}

			if (String.Compare (KnownProperties.AndroidSupportedTargetAotAbis, args.Name, StringComparison.Ordinal) == 0) {
				targetAotAbis = null;
			}
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> names an enabled Android device ABI target.
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetJit"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsTargetJitAbiEnabled (string abiName)
		{
			PopulateTargetJitAbis ();
			return IsAbiEnabled (abiName, targetJitAbis);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> names an enabled host OS ABI target.
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.HostJit"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsHostJitAbiEnabled (string	abiName)
		{
			PopulateHostJitAbis ();
			return IsAbiEnabled (abiName, hostJitAbis);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> names an enabled AOT cross-compieler target.
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsTargetAotAbiEnabled (string abiName)
		{
			PopulateTargetAotAbis ();
			return IsAbiEnabled (abiName, targetAotAbis);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS AOT cross compiler ABI
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsHostAotAbi (string abiName)
		{
			return AbiNames.AllHostAotAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a Windows AOT cross compiler ABI
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsWindowsAotAbi (string abiName)
		{
			return AbiNames.AllWindowsAotAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a 64-bit Android JIT ABI target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is64BitTargetJitAbi (string abiName)
		{
			return AbiNames.All64BitTargetJitAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a 32-bit Android JIT ABI target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is32BitTargetJitAbi (string abiName)
		{
			return AbiNames.All32BitTargetJitAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a 64-bit AOT cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is64BitTargetAotAbi (string abiName)
		{
			return AbiNames.All64BitTargetAotAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a 32-bit AOT cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is32BitTargetAotAbi (string abiName)
		{
			return AbiNames.All32BitTargetAotAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS Windows cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsMingwHostAbi (string abiName)
		{
			return AbiNames.AllMingwHostAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS Windows 32-bit cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is32BitMingwHostAbi (string abiName)
		{
			return AbiNames.All32BitMingwHostAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS Windows 64-bit cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is64BitMingwHostAbi (string abiName)
		{
			return AbiNames.All64BitMingwHostAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS Windows cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsNativeHostAbi (string abiName)
		{
			return AbiNames.AllNativeHostAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS Windows AOT cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsCrossAotWindowsAbi (string abiName)
		{
			return AbiNames.AllCrossWindowsAotAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS AOT 64-bit cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is64BitCrossAbi (string abiName)
		{
			return AbiNames.All64BitCrossAotAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS Windows 32-bit cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is32BitCrossAbi (string abiName)
		{
			return AbiNames.All32BitCrossAotAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS AOT cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsHostCrossAotAbi (string abiName)
		{
			return AbiNames.AllCrossHostAotAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS Windows AOT cross-compiler target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsWindowsCrossAotAbi (string abiName)
		{
			return AbiNames.AllCrossWindowsAotAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a Windows LLVM target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsLlvmWindowsAbi (string abiName)
		{
			return AbiNames.AllLlvmWindowsAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a host OS LLVM target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool IsLlvmHostAbi (string abiName)
		{
			return AbiNames.AllLlvmHostAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a 32-bit LLVM target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is32BitLlvmAbi (string abiName)
		{
			return AbiNames.All32BitLlvmAbis.Contains (abiName);
		}

		/// <summary>
		///   Checks whether <paramref name="abiName"/> refers to a 64-bit LLVM target
		///   <seealso cref="Xamarin.Android.Prepare.AbiNames.TargetAot"/>
		///   <seealso cref="Xamarin.Android.Prepare.Abi"/>
		/// </summary>
		public bool Is64BitLlvmAbi (string abiName)
		{
			return AbiNames.All64BitLlvmAbis.Contains (abiName);
		}

		bool IsAbiEnabled (string abiName, HashSet<string>? collection)
		{
			if (collection == null)
				return false;

			if (String.IsNullOrEmpty (abiName))
				throw new ArgumentException ("must not be null or empty", nameof (abiName));

			return collection.Contains (abiName);
		}

		void PopulateTargetJitAbis ()
		{
			if (targetJitAbis != null)
				return;

			Utilities.AddAbis (Properties.GetRequiredValue (KnownProperties.AndroidSupportedTargetJitAbis).Trim (), ref targetJitAbis);
		}

		void PopulateTargetAotAbis ()
		{
			if (targetAotAbis != null)
				return;

			Utilities.AddAbis (Properties.GetRequiredValue (KnownProperties.AndroidSupportedTargetAotAbis).Trim (), ref targetAotAbis);
		}

		void PopulateHostJitAbis ()
		{
			if (hostJitAbis != null) {
				return;
			}
			Utilities.AddAbis (Properties.GetRequiredValue (KnownProperties.AndroidSupportedHostJitAbis).Trim (), ref hostJitAbis);
		}

		void OnPropertiesChanged (object sender, EventArgs args)
		{
			hostJitAbis = null;
		}

		/// <summary>
		///   Construct and return path to a log file other than the main log file. The <paramref name="tags"/> parameter
		///   is a string appended to the log name - it MUST consist only of characters valid for file/path names.
		/// </summary>
		public string GetLogFilePath (string tags)
		{
			return GetLogFilePath (tags, false);
		}

		string GetLogFilePath (string? tags, bool mainLogFile)
		{
			string logFileName;
			if (String.IsNullOrEmpty (tags)) {
				if (!mainLogFile)
					throw new ArgumentException ("must not be null or empty", nameof (tags));
				logFileName = $"{Configurables.Defaults.LogFilePrefix}-{BuildTimeStamp}.log";
			} else {
				logFileName = $"{Configurables.Defaults.LogFilePrefix}-{BuildTimeStamp}.{tags}.log";
			}

			return Path.Combine (LogDirectory, logFileName);
		}

		/// <summary>
		///   Check value of a condition flag. If the flag was never set, it will return <c>false</c>
		/// </summary>
		public bool CheckCondition (KnownConditions knownCondition)
		{
			if (!conditions.TryGetValue (knownCondition, out bool v))
				return false;

			return v;
		}

		/// <summary>
		///   Set a condition flag that can be used by any part of the program for its own purposes. <see cref="KnownConditions"/>
		/// </summary>
		public void SetCondition (KnownConditions knownCondition, bool v)
		{
			Log.DebugLine ($"Setting condition {knownCondition} to '{v}'");
			conditions [knownCondition] = v;
		}

		/// <summary>
		///   Initialize the execution context. Called only once from Main.
		/// </summary>
		public async Task<bool> Init (string? scenarioName = null)
		{
			SetCondition (KnownConditions.AllowProgramInstallation, true);

			characters = Characters.Create (this);

			Log.StatusLine ("Main log file: ", MainLogFilePath, ConsoleColor.Gray, Log.DestinationColor);

			Banner ("Detecting operating system");
			InitOS ();

			Log.StatusLine ();
			Log.StatusLine ("     OS type: ", OS.Type, tailColor: Log.InfoColor);
			Log.StatusLine ("   OS flavor: ", OS.Flavor, tailColor: Log.InfoColor);
			Log.StatusLine ("     OS name: ", OS.Name, tailColor: Log.InfoColor);
			Log.StatusLine ("  OS release: ", OS.Release, tailColor: Log.InfoColor);
			Log.StatusLine ("     OS bits: ", OS.Architecture, tailColor: Log.InfoColor);
			Log.StatusLine ("   CPU count: ", OS.CPUCount.ToString (), tailColor: Log.InfoColor);
			Log.StatusLine ("   Disk Info: ", string.Empty, tailColor: Log.InfoColor);
			Log.StatusLine (string.Empty, OS.DiskInformation, tailColor: Log.InfoColor);
			Log.StatusLine ();

			if (EnableAllTargets) {
				Properties.Set (KnownProperties.AndroidSupportedTargetJitAbis, Utilities.ToXamarinAndroidPropertyValue (AbiNames.AllJitAbis));
				Properties.Set (KnownProperties.AndroidSupportedHostJitAbis, Utilities.ToXamarinAndroidPropertyValue (AbiNames.AllHostAbis));
				Properties.Set (KnownProperties.AndroidSupportedTargetAotAbis, Utilities.ToXamarinAndroidPropertyValue (AbiNames.AllAotAbis));
			}

			DiscoverScenarios (scenarioName);

			if (!await OS.Init ()) {
				Log.ErrorLine ("Failed to initialize OS support");
				return false;
			}

			Log.StatusLine ();

			Tools.Init (this);

			if (SelectedScenario.NeedsGitSubmodules) {
				Banner ("Updating Git submodules");

				var git = new GitRunner (this);
				if (!await git.SubmoduleUpdate ()) {
					Log.ErrorLine ("Failed to update Git submodules");
					return false;
				}
			}

			BuildInfo = new BuildInfo ();
			if (SelectedScenario.NeedsGitBuildInfo) {
				await BuildInfo.GatherGitInfo (this);
			}

			AbiNames.LogAllNames (this);

			if (MakeConcurrency == 0)
				MakeConcurrency = OS.CPUCount + 1;

			return true;
		}

		/// <summary>
		///   Execute the selected scenario (either the default one or one chosen by using the <c>-s</c> command line parameter)
		/// </summary>
		public async Task<bool> Execute ()
		{
			Scenario scenario = SelectedScenario;
			Banner ($"Running scenario: {scenario.Description}");

			string logFilePath = scenario.LogFilePath ?? mainLogFilePath;
			Log? scenarioLog = null;

			if (!String.IsNullOrEmpty (scenario.LogFilePath)) {
				Log.StatusLine ("Log file: ", scenario.LogFilePath!, ConsoleColor.Gray, Log.DestinationColor);
				scenarioLog = new Log (logFilePath);
			} else {
				Log.StatusLine ("Logging to main log file");
			}

			try {
				await scenario.Run (this, scenarioLog);
			} finally {
				scenarioLog?.Dispose ();
			}

			return true;
		}

		void DiscoverScenarios (string? scenarioName)
		{
			List<Type> types = Utilities.GetTypesWithCustomAttribute<ScenarioAttribute> ();

			bool haveScenarioName = !String.IsNullOrEmpty (scenarioName);
			Scenario? selectedScenario = null;
			Scenario? detectedDefaultScenario = null;
			foreach (Type type in types) {
				var scenario = Activator.CreateInstance (type) as Scenario;
				if (scenario == null)
					throw new InvalidOperationException ($"Scenario not derived from the Scenario type ({type})");

				Scenarios.Add (scenario.Name, scenario);
				if (IsDefaultScenario (type)) {
					if (detectedDefaultScenario != null)
						throw new InvalidOperationException ($"Only one default scenario is allowed. {detectedDefaultScenario} was previously declared as one, {type} is also marked as deafult");
					detectedDefaultScenario = scenario;
				}

				if (!haveScenarioName || selectedScenario != null)
					continue;

				if (String.Compare (scenarioName, scenario.Name, StringComparison.OrdinalIgnoreCase) != 0)
					continue;

				selectedScenario = scenario;
			}

			if (haveScenarioName && selectedScenario == null)
				throw new InvalidOperationException ($"Unknown scenario '{scenarioName}'");

			if (selectedScenario == null)
				selectedScenario = detectedDefaultScenario;

			if (selectedScenario == null)
				throw new InvalidOperationException ("No specific scenario named and no default scenario found");

			defaultScenario = detectedDefaultScenario!;
			SelectedScenario = selectedScenario;

			Log.DebugLine ($"Initializing scenario {SelectedScenario.Name}");
			SelectedScenario.Init (this);
		}

		bool IsDefaultScenario (Type type)
		{
			foreach (ScenarioAttribute attr in type.GetCustomAttributes (typeof(ScenarioAttribute), true)) {
				if (attr.IsDefault)
					return true;
			}

			return false;
		}

		/// <summary>
		///   Print a "banner" to the output stream - will not show anything only if logging verbosity is set to <see
		///   cref="LoggingVerbosity.Quiet"/>
		/// </summary>
		public void Banner (string text)
		{
			if (LoggingVerbosity <= LoggingVerbosity.Quiet)
				return;

			Log.StatusLine ();
			Log.StatusLine ("=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=", BannerColor);
			Log.StatusLine (text, BannerColor);
			Log.StatusLine ("=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=", BannerColor);
			Log.StatusLine ();
		}

		string GetLogDirectory ()
		{
			if (!String.IsNullOrEmpty (overridenLogDirectory))
				return overridenLogDirectory!;

			if (!String.IsNullOrEmpty (logDirectory))
				return logDirectory!;

			logDirectory = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "bin", $"Build{Configuration}");
			if (!Directory.Exists (logDirectory))
				Directory.CreateDirectory (logDirectory);

			return logDirectory;
		}
	}
}
