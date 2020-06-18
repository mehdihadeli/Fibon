#!/bin/bash
# problem in creating out dir in .net 2 and 3
# https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish
dotnet publish ./src/Fibon.Api  -c Release -o ./src/Fibon.Api/bin/Docker  
dotnet publish ./src/Fibon.Service  -c Release -o ./src/Fibon.Service/bin/Docker  