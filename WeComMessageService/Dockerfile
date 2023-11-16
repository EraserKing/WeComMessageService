#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "/src/WeComMessageService/WeComMessageService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
EXPOSE 80
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WeComMessageService.dll"]