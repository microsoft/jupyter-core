
# Microsoft.Jupyter.Core Preview #

The **Microsoft.Jupyter.Core** library makes it easier to write language kernels for Jupyter using .NET Core languages like C# and F#.
This library uses .NET Core technologies such as the [ASP.NET Core Dependency Injection Framework](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.2) to help make it straightforward to develop for the Jupyter platform.

Kernels developed using **Microsoft.Jupyter.Core** can be installed as [.NET Core Global Tools](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create).
This makes it easy to package, distribute, and install language kernels.
For instance, the [**IEcho**](examples/echo-kernel/) sample kernel can be installed into a user's Jupyter environment with two commands:

```
cd examples/echo-kernel/
dotnet run -- install
```

Once installed, the IEcho example can then be used like any other Jupyter kernel by running your favorite client:

```
jupyter notebook
```

After a language kernel has been published as a NuGet package, it can be installed into a user's Jupyter environment with two commands. For example, if the IEcho kernel were published as a NuGet package named `Microsoft.Jupyter.Example.IEcho`, it could be installed via the following commands:

```
dotnet tool install -g Microsoft.Jupyter.Example.IEcho
dotnet iecho install
````

## Making New Language Kernels ##

Using  **Microsoft.Jupyter.Core** to make a new language kernel follows in several steps:

- Create a new console application project in your favorite .NET Core language.
- Add properties to your project to enable packaging as a [.NET Core Global Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create).
- Add `Microsoft.Jupyter.Core` as a package reference to your new project.
- Add metadata properties for your new kernel.
- Subclass the `BaseEngine` class.
- Pass your metadata and engine to the `KernelApplication` class.

Each of these steps has been demonstrated in the example kernels provided with the library:

- [**IEcho**](examples/echo-kernel/): A simple language kernel that echos its input back as output.
- [**IMoon**](examples/moon-kernel/): A language kernel for the [MoonSharp](http://moonsharp.org/) dialect of Lua.

For more details, see the [provided tutorial](tutorial.md).

## Contributing ##

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.


