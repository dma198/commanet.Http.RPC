#!/bin/bash
dotnet build -c release ./RPC.Common
dotnet build -c release ./RPC.Server
dotnet build -c release ./RPC.Client
nuget pack ./package/Package.nuspec
