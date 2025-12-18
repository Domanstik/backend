# 1. Используем образ SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 2. Копируем файлы проектов (чтобы восстановить зависимости)
COPY ["StarMarathon.API/StarMarathon.API.csproj", "StarMarathon.API/"]
COPY ["StarMarathon.Application/StarMarathon.Application.csproj", "StarMarathon.Application/"]
COPY ["StarMarathon.Domain/StarMarathon.Domain.csproj", "StarMarathon.Domain/"]
COPY ["StarMarathon.Infrastructure/StarMarathon.Infrastructure.csproj", "StarMarathon.Infrastructure/"]

# 3. Восстанавливаем пакеты
RUN dotnet restore "StarMarathon.API/StarMarathon.API.csproj"

# 4. Копируем остальной код
COPY . .

# 5. Собираем проект (Release)
WORKDIR "/src/StarMarathon.API"
RUN dotnet publish "StarMarathon.API.csproj" -c Release -o /app/publish

# 6. Финальный образ (только для запуска, легкий)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render ожидает, что приложение слушает порт, который он выдаст в переменной PORT
# Но ASP.NET по умолчанию слушает 8080. Мы настроим это через ENV.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "StarMarathon.API.dll"]