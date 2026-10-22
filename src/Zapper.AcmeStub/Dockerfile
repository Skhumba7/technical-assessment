FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY Directory.Build.props ./
COPY src/Zapper.AcmeStub/Zapper.AcmeStub.csproj src/Zapper.AcmeStub/

RUN dotnet restore src/Zapper.AcmeStub/Zapper.AcmeStub.csproj

COPY src/Zapper.AcmeStub/ src/Zapper.AcmeStub/
RUN dotnet publish src/Zapper.AcmeStub/Zapper.AcmeStub.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Zapper.AcmeStub.dll"]
