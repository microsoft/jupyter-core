# Tutorial #

In this tutorial, we'll walk through how the [**IEcho**](examples/echo-kernel/) language kernel was created, as an example of how to make your own language kernels for Jupyter.
We'll use C# to write our kernel, but the same process can be used with your favorite .NET Core language (e.g.: F# or VB.NET).

## Prerequisites ##

Before proceeding with the tutorial, you'll need to make sure you've installed a couple things first:

- the .NET Core SDK (version 2.1 or later).
- a working Jupyter client, such as Jupyter Notebook. We recommend installing Jupyter Notebook with the Anaconda distribution of Python.

## Creating the Kernel Project ##

First, you'll need a new project for your language kernel.
Since your kernel will be run by Jupyter as a command-line application, we need to use a console application template.
Using the .NET Core SDK, run the following command:

```
dotnet new console -lang C# --name "IEcho"
```

This will create a new folder called `IEcho` with two files, `IEcho.csproj` and `Program.cs`.
We will need to add some properties to `IEcho.csproj` to let the .NET Core SDK package our new kernel as a global tool so that users can install it.
Open up the project file and make sure that the following properties are in the first `<PropertyGroup>` element:

```xml
<ToolCommandName>dotnet-iecho</ToolCommandName>
<PackAsTool>True</PackAsTool>
<OutputType>Exe</OutputType>
<TargetFramework>netcoreapp2.1</TargetFramework>
```

Next, we need to add a package reference to `Microsoft.Jupyter.Core` to the project.
Run the following command from the `IEcho` directory:

```
dotnet add package Microsoft.Jupyter.Core
```

This should be everything we need from our project file, so let's move on to the code!

## Setting up the Kernel Application ##

Our new language kernel needs to expose a command that Jupyter can call to start up the various servers that make up the kernel.
Moreover, users will need to be able to run an install command to add the kernel to Jupyter's list of valid kernels.
Both of these commands can be implemented using the `KernelApplication` class provided with the **Microsoft.Jupyter.Core** library.
Before we use `KernelApplication`, we need to get two pieces of code ready:

- Metadata properties defining the name of our new kernel, the language supported, etc.
- A class implementing the `IExecutionEngine` interface that sends code to the interpreter for our language.

Dealing with each in turn, make a new file called `KernelProperties.cs` with the following contents:

```csharp
namespace Microsoft.Jupyter.Core
{

    internal static class Constants
    {
        internal static KernelProperties PROPERTIES = new KernelProperties
        {
            // Name of the kernel as it appears outside of code.
            FriendlyName = "IEcho",
            // A short name for the kernel to be used in code, such as when
            // calling from jupyter_client.
            KernelName = "iecho",
            // The version of the kernel.
            KernelVersion = "0.0.1",
            // Name of the kernel as it should appear in lists of available kernels.
            DisplayName = "Echo",

            // Name of the language implemented by the kernel.
            // Note that this property is used to set the syntax highlighting
            // mode in some clients, such as Jupyter Notebook.
            LanguageName = "Echo",
            // Version of the language implemeted by the kernel.
            LanguageVersion = "0.1",
            // The MIME type of the language implemented by the kernel.
            // This property is used mainly for providing "plain" downloads from
            // Jupyter clients.
            LanguageMimeType = "text/plain",
            // The file extension for source files written in the language
            // implemented by the kernel.
            // This property is used mainly for providing "plain" downloads from
            // Jupyter clients.
            LanguageFileExtension = ".txt",

            // An extended description of the kernel.
            Description = "A simple kernel that echos its input."
        };
    }
}
```

Next, let's make a new class that implements `IExecutionEngine`.
The easiest way to do this is to subclass from the `BaseEngine` abstract class.
Make a new file called `EchoEngine.cs` and add the following code:

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Microsoft.Jupyter.Core
{
    public class EchoEngine : BaseEngine
    {
        public EchoEngine(
            IShellServer shell,
            IShellRouter router,
            IOptions<KernelContext> context,
            ILogger<EchoEngine> logger,
            IServiceProvider serviceProvider
        ) : base(shell, router, context, logger, serviceProvider) { }
        
        public override async Task<ExecutionResult> ExecuteMundane(string input, IChannel channel)
        {
            return input.ToExecutionResult();
        }
    }
}
```

Here, the `ExecuteMundane` method is used to run cells that don't contain any magic commands.
Execution results (formatted as `Out[1]` in Jupyter Notebook, for instance) are returned to the **Microsoft.Jupyter.Core** library as values of the `ExecutionResult` struct.
The library provides the `ToExecutionResult` extension method for `string` and `IEnumerable<object>` that makes it easier to generate an `ExecutionResult` from a value.

Now that we have everything that we need to start up a kernel application, we can modify `Program.cs` as follows:

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;
using static Microsoft.Jupyter.Core.Constants;

namespace Microsoft.Jupyter.Core
{
    class Program
    {
        public static void Init(ServiceCollection serviceCollection) =>
            serviceCollection
                // Start a new service for the ExecutionEngine.
                .AddSingleton<IExecutionEngine, EchoEngine>();

        public static int Main(string[] args) {
            var app = new KernelApplication(
                PROPERTIES,
                Init
            );

            return app.WithDefaultCommands().Execute(args);
        }
    }
}
```

The first method, `Init`, configures the ASP.NET Core Dependency Injection Framework to make `EchoEngine` available to any parts of our kernel that require access to an `IExecutionEngine`.
In this method, we're given a `ServiceCollection` to register everything we need.
For this kernel, we'll only need the engine itself, but a more realistic kernel may need one or more other services here, or may configure loggers and take in configuration options.
Note that since we've added `EchoEngine` as a singleton, the entire kernel will share a single instance for its entire lifetime (typically until restarted or shutdown by a Jupyter client).

The second method, `Main`, creates the actual kernel application when called at the command line, passing along the metadata properties and an `Action` that 
configures dependency injection for our language kernel. We then call the `WithDefaultCommands` method so the kernel application can register it can
handle commands like `install` and `kernel`.
Finally, we call the kernel application with our command line arguments to hand control off to **Microsoft.Jupyter.Core**.

## Running the New Kernel ##

Our new language kernel can be installed in one of two ways, development mode, or as a global tool.
Development mode will cause the .NET Core SDK to automatically recompile the kernel whenever a new kernel instance is started.
To install our new kernel in development mode, use `dotnet run` to run our new kernel application:

```
dotnet run -- install --develop
```

Note that we need the `--` to indicate which arguments go to the `dotnet` command and which arguments are sent to our kernel program itself.
Once installed in this way, run your favorite Jupyter client to use the new kernel.
For instance:

```
jupyter notebook
```

This is useful for prototyping and experimenting during development, but can be confusing as a distribution method.
On the other hand, by packing our new kernel as a global tool, we can make it easy for users to get up and running.

First, package up your new kernel as a NuGet package:

```
dotnet pack
```

This should make a new NuGet package (`*.nupkg`) under `bin/Debug`.
We can install that package as a global tool to get up and running:

```
dotnet tool install --global IEcho --add-source bin/Debug
```

Here, we needed to pass the `--add-source` option as we haven't uploaded the new package to nuget.org yet; for actual distribution to users, you'll want to upload your package so that the `--add-source` option isn't needed.

Note that if you already have a global tool with that name, you'll need to uninstall it first.
In any case, you can now run your new kernel as a subcommand of the `dotnet` command.
For instance, to install your new kernel into Jupyter:

```
dotnet iecho install
```

That's it, you've got everything you need to start making new kernels!

## Debugging ##

Once you have your new kernel going, it will often be helpful to inspect how it works in practice to assist in tracking down bugs.
One way to do so is to turn on more detailed logging when Jupyter starts your kernel.
Jupyter finds what kernels are available by using a collection of small JSON files, called *kernel specs*.
To get a list of what kernel specs are available, use the `jupyter` command:

```
$ jupyter kernelspec list
Available kernels:
  python3       C:\Users\<username>\AppData\Roaming\Local\Continuum\Anaconda2\share\jupyter\kernels\python3
  iecho         C:\ProgramData\jupyter\kernels\iecho
  imoon         C:\ProgramData\jupyter\kernels\imoon
```

To enable fine-grained logging, open the `kernel.json` file in the directory for your kernel and add the arguments `"--log-level", "trace"`.
For instance, if you wanted to trace the IEcho example kernel, your kernel spec might look something like this:

```json
{
    "argv": [
        "dotnet",
        "iecho",
        "kernel",
        "{connection_file}",
        "--log-level",
        "trace"
    ],
    "display_name": "Echo",
    "language": "Echo"
}
```

The other approach to diagnosing problems with your kernel is to attach a .NET Core debugger.
Since Jupyter launches each kernel on your behalf, you won't be able to start a debugging session from within your IDE, but both Visual Studio and Visual Studio Code make it easy to attach debuggers to a process that is already running.
For more resources, see the links below:

- [Attaching to running processes with the Visual Studio debugger](https://docs.microsoft.com/en-us/visualstudio/debugger/attach-to-running-processes-with-the-visual-studio-debugger?view=vs-2017)
- [Getting Started with C# and Visual Studio Code](https://docs.microsoft.com/en-us/dotnet/core/tutorials/with-visual-studio-code#debug)
