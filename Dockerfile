# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore theo từng project để tận dụng cache layer.
COPY AnimeStreamingApi.sln ./
COPY src/Anime.Api/Anime.Api.csproj src/Anime.Api/
COPY src/Anime.Application/Anime.Application.csproj src/Anime.Application/
COPY src/Anime.Domain/Anime.Domain.csproj src/Anime.Domain/
COPY src/Anime.Infrastructure/Anime.Infrastructure.csproj src/Anime.Infrastructure/
RUN dotnet restore src/Anime.Api/Anime.Api.csproj

# Copy toàn bộ mã nguồn rồi publish.
COPY . .
RUN dotnet publish src/Anime.Api/Anime.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Render đặt biến PORT; Program.cs đọc PORT để bind 0.0.0.0. Mặc định 8080 khi chạy thủ công.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Anime.Api.dll"]
