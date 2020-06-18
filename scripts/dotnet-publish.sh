cd ./src/Fibon.Api
dotnet publish  -c Release -o ./bin/Docker
dotnet publish ./src/Fibon.Service -c Release -o ./src/Fibon.Service/bin/Docker