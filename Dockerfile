FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
# .NET 10 detects Blazor framework assets from Razor files during restore.
# Copy the complete source first so blazor.web.js is included in the package.
COPY src/HistoriaMedyczna/ src/HistoriaMedyczna/
RUN dotnet restore src/HistoriaMedyczna/HistoriaMedyczna.csproj
RUN dotnet publish src/HistoriaMedyczna/HistoriaMedyczna.csproj -c Release -o /published --no-restore /p:UseAppHost=false
RUN test -s /published/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /published .
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "HistoriaMedyczna.dll"]
