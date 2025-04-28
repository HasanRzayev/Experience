# 1. Build mərhələsi
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 2. Layihəni köçür
COPY . .

# 3. Əsas proyektin olduğu yerə gir və publish et
WORKDIR /src/ExperienceProject
RUN dotnet publish -c Release -o /app/publish

# 4. Runtime mərhələsi
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# 5. PORT təyin et
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

# 6. API-nı işə sal
ENTRYPOINT ["dotnet", "ExperienceProject.dll"]
