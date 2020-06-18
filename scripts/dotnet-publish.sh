#!/bin/bash
# dotnet publish ./src/Fibon.Api -c Release -o ./bin/Docker
# dotnet publish ./src/Fibon.Service -c Release -o ./bin/Docker

dotnet publish ./src/Fibon.Api  -c Release -o ./src/Fibon.Api/bin/Docker  