# ============================================
# Этап 1: Сборка и публикация приложения
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файлы проектов для восстановления зависимостей
COPY ["InternetShop.Server/InternetShop.Server.csproj", "InternetShop.Server/"]
COPY ["InternetShop.Shared/InternetShop.Shared.csproj", "InternetShop.Shared/"]

# Восстанавливаем зависимости
RUN dotnet restore "InternetShop.Server/InternetShop.Server.csproj"

# Копируем весь исходный код
COPY . .

# Собираем и публикуем приложение
WORKDIR "/src/InternetShop.Server"
RUN dotnet publish "InternetShop.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ============================================
# Этап 2: Запуск приложения
# ============================================
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Копируем опубликованные файлы из этапа сборки
COPY --from=build /app/publish .

# Документируем порт, который использует сервер
EXPOSE 3000

# Команда запуска контейнера
ENTRYPOINT ["dotnet", "InternetShop.Server.dll"]