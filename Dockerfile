FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY Directory.Packages.props SqlServerMCP.sln README.md ./
COPY SqlServerMCP/SqlServerMCP.csproj SqlServerMCP/

RUN dotnet restore SqlServerMCP/SqlServerMCP.csproj

COPY . .
RUN dotnet publish SqlServerMCP/SqlServerMCP.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV MCP_MODE=http
ENV MCP_HTTP_URL=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SqlServerMCP.dll"]
