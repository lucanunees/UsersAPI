# ============================================
# Stage 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar arquivos de projeto e restaurar dependências
COPY ["src/UsersAPI.Web/UsersAPI.Web.csproj", "UsersAPI.Web/"]
COPY ["src/UsersAPI.Domain/UsersAPI.Domain.csproj", "UsersAPI.Domain/"]
COPY ["src/UsersAPI.Infra/UsersAPI.Infra.csproj", "UsersAPI.Infra/"]
RUN dotnet restore "UsersAPI.Web/UsersAPI.Web.csproj"

# Copiar código fonte
COPY src/ .

# Build da aplicação
WORKDIR "/src/UsersAPI.Web"
RUN dotnet build "UsersAPI.Web.csproj" -c Release -o /app/build

# ============================================
# Stage 2: Publish
# ============================================
FROM build AS publish
RUN dotnet publish "UsersAPI.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ============================================
# Stage 3: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Criar usuário não-root
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copiar binários publicados
COPY --from=publish /app/publish .

# Expor porta
EXPOSE 80
EXPOSE 443

# Trocar para usuário não-root
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "UsersAPI.Web.dll"]
