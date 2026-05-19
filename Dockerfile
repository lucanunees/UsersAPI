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

ENTRYPOINT ["dotnet", "UsersAPI.Web.dll"]