# ── build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer-cache-friendly restore
COPY src/AuthCore.API/AuthCore.API.csproj                         src/AuthCore.API/
COPY src/AuthCore.Application/AuthCore.Application.csproj         src/AuthCore.Application/
COPY src/AuthCore.Domain/AuthCore.Domain.csproj                   src/AuthCore.Domain/
COPY src/AuthCore.Infrastructure/AuthCore.Infrastructure.csproj   src/AuthCore.Infrastructure/

RUN dotnet restore src/AuthCore.API/AuthCore.API.csproj

# Copy full source and publish
COPY src/ src/
RUN dotnet publish src/AuthCore.API/AuthCore.API.csproj \
    -c Release -o /app/publish --no-restore

# ── runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AuthCore.API.dll"]
