
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["MoonMQ.Hosting/MoonMQ.Hosting.csproj", "MoonMQ.Hosting/"]
COPY ["MoonMQ.Core/MoonMQ.Core.csproj", "MoonMQ.Core/"]
RUN dotnet restore "MoonMQ.Hosting/MoonMQ.Hosting.csproj"
COPY . .
WORKDIR "/src/MoonMQ.Hosting"
RUN dotnet build "MoonMQ.Hosting.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MoonMQ.Hosting.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MoonMQ.Hosting.dll"]