FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy Directory.Build.props and project files for layer caching
COPY Directory.Build.props ./
COPY src/Zapper.Payments.Domain/Zapper.Payments.Domain.csproj src/Zapper.Payments.Domain/
COPY src/Zapper.Payments.Application/Zapper.Payments.Application.csproj src/Zapper.Payments.Application/
COPY src/Zapper.Payments.Infrastructure/Zapper.Payments.Infrastructure.csproj src/Zapper.Payments.Infrastructure/
COPY src/Zapper.Payments.Api/Zapper.Payments.Api.csproj src/Zapper.Payments.Api/

RUN dotnet restore src/Zapper.Payments.Api/Zapper.Payments.Api.csproj

# Copy remaining source code and publish
COPY src/ src/
RUN dotnet publish src/Zapper.Payments.Api/Zapper.Payments.Api.csproj -c Release -o /out --no-restore

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Zapper.Payments.Api.dll"]
