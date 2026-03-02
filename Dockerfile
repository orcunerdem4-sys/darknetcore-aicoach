FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Proje dosyasını kopyala ve restore et
COPY ["DarkNetCore.csproj", "./"]
RUN dotnet restore "DarkNetCore.csproj"

# Tüm dosyaları kopyala ve yayınla
COPY . .
RUN dotnet publish "DarkNetCore.csproj" -c Release -o /app/publish

# Çalışma zamanı imajı
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Port ayarı (Render genelde PORT environment variable kullanır)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DarkNetCore.dll"]
