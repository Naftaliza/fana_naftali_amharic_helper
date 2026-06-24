# Multi-stage build for the ASP.NET Core 9 API.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY AmharicHelper.sln ./
COPY src/AmharicHelper.Domain/*.csproj src/AmharicHelper.Domain/
COPY src/AmharicHelper.Application/*.csproj src/AmharicHelper.Application/
COPY src/AmharicHelper.Infrastructure/*.csproj src/AmharicHelper.Infrastructure/
COPY src/AmharicHelper.Api/*.csproj src/AmharicHelper.Api/
COPY tests/AmharicHelper.UnitTests/*.csproj tests/AmharicHelper.UnitTests/
RUN dotnet restore src/AmharicHelper.Api/AmharicHelper.Api.csproj

COPY . .
RUN dotnet publish src/AmharicHelper.Api/AmharicHelper.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AmharicHelper.Api.dll"]
