# ЭТАП 1: Сборка (Build)
# Используем образ SDK для компиляции кода
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. Копируем файлы проектов (.csproj)
# Мы копируем их отдельно перед остальным кодом, чтобы кэшировать зависимости (NuGet packages).
# Если ты поменяешь код, но не добавишь новые библиотеки, этот шаг пропустится и сборка будет быстрее.
COPY ["StarMarathon.API/StarMarathon.API.csproj", "StarMarathon.API/"]
COPY ["StarMarathon.Application/StarMarathon.Application.csproj", "StarMarathon.Application/"]
COPY ["StarMarathon.Domain/StarMarathon.Domain.csproj", "StarMarathon.Domain/"]
COPY ["StarMarathon.Infrastructure/StarMarathon.Infrastructure.csproj", "StarMarathon.Infrastructure/"]

# 2. Восстанавливаем зависимости
RUN dotnet restore "StarMarathon.API/StarMarathon.API.csproj"

# 3. Копируем остальной исходный код
COPY . .

# 4. Собираем и публикуем проект (Release)
WORKDIR "/src/StarMarathon.API"
RUN dotnet publish "StarMarathon.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ЭТАП 2: Запуск (Runtime)
# Используем легкий образ ASP.NET Core только для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Копируем собранные файлы из этапа сборки
COPY --from=build /app/publish .

# Настройка порта для Render.com
# Render обычно ищет порт 8080 или тот, что в переменной PORT.
# Мы явно говорим .NET слушать 8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Запускаем API (Бот запустится автоматически, т.к. он BackgroundService)
ENTRYPOINT ["dotnet", "StarMarathon.API.dll"]