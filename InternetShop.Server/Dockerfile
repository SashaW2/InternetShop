FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["InternetShop.Server/InternetShop.Server.csproj", "InternetShop.Server/"]
COPY ["InternetShop.Shared/InternetShop.Shared.csproj", "InternetShop.Shared/"]

RUN dotnet restore "InternetShop.Server/InternetShop.Server.csproj"

COPY . .

WORKDIR "/src/InternetShop.Server"
RUN dotnet publish "InternetShop.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 3000

ENTRYPOINT ["dotnet", "InternetShop.Server.dll"]