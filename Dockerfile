# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/DailyGourmet.Domain/DailyGourmet.Domain.csproj src/DailyGourmet.Domain/
COPY src/DailyGourmet.Application/DailyGourmet.Application.csproj src/DailyGourmet.Application/
COPY src/DailyGourmet.Infrastructure/DailyGourmet.Infrastructure.csproj src/DailyGourmet.Infrastructure/
COPY src/DailyGourmet.Api/DailyGourmet.Api.csproj src/DailyGourmet.Api/
RUN dotnet restore src/DailyGourmet.Api/DailyGourmet.Api.csproj

COPY src/ src/
RUN dotnet publish src/DailyGourmet.Api/DailyGourmet.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd --uid 5678 --user-group --no-create-home --shell /usr/sbin/nologin appuser
COPY --from=build /app .
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DailyGourmet.Api.dll"]
