# IEcho Example Kernel #

This project provides an example of how to write your own Jupyter kernels using Microsoft.Jupyter.Core.
The kernel implemented by this project is a simple "echo" kernel that repeats its input as output.

## Installing from NuGet.org ##

```
dotnet tool install -g Microsoft.Jupyter.Example.IEcho
dotnet iecho install
```

## Installing from a Local Build ##

```
cd echo-kernel
dotnet pack
dotnet tool install -g Microsoft.Jupyter.Example.IEcho --add-source .\bin\Debug\
dotnet iecho install
```

## Installing for Local Development ##

```
cd echo-kernel
dotnet run -- install --develop
```
