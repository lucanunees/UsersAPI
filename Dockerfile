# Estágio 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia e restaura dependências
COPY ["src/UsersAPI.Web/UsersAPI.Web.csproj", "src/UsersAPI.Web/"]
COPY ["src/UsersAPI.Infra/UsersAPI.Infra.csproj", "src/UsersAPI.Infra/"]
COPY ["src/UsersAPI.Domain/UsersAPI.Domain.csproj", "src/UsersAPI.Domain/"]

RUN dotnet restore "src/UsersAPI.Web/UsersAPI.Web.csproj"

# Copia tudo
COPY . .

# Publica
RUN dotnet publish "src/UsersAPI.Web/UsersAPI.Web.csproj" -c Release -o /app/publish

# Estágio 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
EXPOSE 443

# Trocar para usuário não-root
RUN useradd -m appuser
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "UsersAPI.Web.dll"]
